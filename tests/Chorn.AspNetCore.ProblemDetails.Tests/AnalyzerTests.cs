namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Analyzers;
using Microsoft.CodeAnalysis;

/// <summary>
/// Verifies the rules that catch at compile time what the type system and the startup cannot.
/// </summary>
public class AnalyzerTests
{
	/// <summary>
	/// A producer every snippet can use, with one problem on it.
	/// </summary>
	private const string Producer = """
		using Chorn.AspNetCore.ProblemDetails;
		using Microsoft.AspNetCore.Http;
		using Microsoft.AspNetCore.Mvc;

		public sealed class MyProblems : ProblemProducerBase
		{
			public static ExplainedProblemDetails Boom => new()
			{
				Title = "Boom",
				Status = StatusCodes.Status409Conflict,
				Type = "/problems/boom",
				Explanation = "Server side only."
			};
		}
		""";

	[Test]
	[Arguments("Results.Problem(MyProblems.Boom)")]
	[Arguments("TypedResults.Problem(MyProblems.Boom)")]
	[Arguments("Results.Json(MyProblems.Boom)")]
	[Arguments("Results.Ok(MyProblems.Boom)")]
	public async Task ProblemHandedToSomethingThatSerializesIt_IsReported(string call)
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), $$"""
			public static class Endpoints
			{
				public static IResult Answer() => {{call}};
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD001");
	}

	[Test]
	[Arguments("new OkObjectResult(MyProblems.Boom)")]
	[Arguments("new JsonResult(MyProblems.Boom)")]
	public async Task ProblemHandedToAResultThatSerializesIt_IsReported(string call)
	{
		// OkObjectResult and its siblings serialize exactly like the ObjectResult they derive from.
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), $$"""
			public sealed class MyController : ControllerBase
			{
				public IActionResult Answer() => {{call}};
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD001");
	}

	[Test]
	[Arguments("ExplainedProblemDetails")]
	[Arguments("System.Threading.Tasks.Task<ExplainedProblemDetails>")]
	[Arguments("ActionResult<ExplainedProblemDetails>")]
	public async Task ActionAnsweringWithTheProblemItself_IsReportedThroughItsWrapper(string returnType)
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), $$"""
			public sealed class MyController : ControllerBase
			{
				public {{returnType}} Answer() => default!;
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD001");
	}

	[Test]
	public async Task MethodThatIsNotAnEndpoint_IsNotReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				[NonAction]
				public ExplainedProblemDetails Helper() => MyProblems.Boom;

				public static ExplainedProblemDetails Shared() => MyProblems.Boom;
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(0);
	}

	[Test]
	public async Task ProducerInAnotherAssembly_IsNotJudged()
	{
		// The compiler sees only its public members across a reference, so an internal problem would look
		// missing. This rule is an error, and an error has to be right.
		List<Diagnostic> reported = await AnalyzerTests.Run(new DocumentedProblemNameAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				[ProducesProblems<global::Chorn.AspNetCore.ProblemDetails.Tests.Domain.DomainProblems>("Nope")]
				public IActionResult Get() => this.Ok();
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(0);
	}

	[Test]
	public async Task ControllerReturningTheProblemItself_IsReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				public IActionResult Naive() => this.Ok(MyProblems.Boom);

				public ExplainedProblemDetails Direct() => MyProblems.Boom;
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(2);
		await Assert.That(reported.All(diagnostic => diagnostic.Id == "CHPD001")).IsTrue();
	}

	[Test]
	public async Task ControllerDeclaringItsOwnProblemOverload_IsReported()
	{
		// An instance method always beats an extension method. If a base controller - or one day the framework
		// itself - declares a Problem that takes a ProblemDetails, calls silently stop reaching this package
		// and start serializing the declared problem whole. That is what this rule is here to notice.
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), """
			public abstract class MyBase : ControllerBase
			{
				public IActionResult Problem(Microsoft.AspNetCore.Mvc.ProblemDetails problem) => this.Ok();
			}

			public sealed class MyController : MyBase
			{
				public IActionResult Answer() => this.Problem(MyProblems.Boom);
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD001");
	}

	[Test]
	public async Task ProblemAnsweredTheWayThePackageMeansIt_IsNotReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ExplanationLeakAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				public IActionResult Thrown() => throw MyProblems.Boom.AsException();

				public IActionResult Returned() => this.Problem(MyProblems.Boom);

				public IResult Plain() => Results.Problem(detail: "no problem object in sight");
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(0);
	}

	[Test]
	public async Task DocumentedNameTheProducerDoesNotDeclare_IsReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new DocumentedProblemNameAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				[ProducesProblems<MyProblems>("Bang")]
				public IActionResult Get() => this.Ok();
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD002");
		await Assert.That(reported[0].GetMessage()).Contains("Boom");
	}

	[Test]
	public async Task DocumentedNameOnEndpointMetadata_IsCheckedAsWell()
	{
		// The minimal api spelling: the attribute is constructed rather than applied, so nothing resolves it
		// before the api document is generated.
		List<Diagnostic> reported = await AnalyzerTests.Run(new DocumentedProblemNameAnalyzer(), """
			public static class Endpoints
			{
				public static object Metadata() => new ProducesProblemsAttribute<MyProblems>("Bang");
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD002");
	}

	[Test]
	public async Task DocumentedNameThatResolves_IsNotReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new DocumentedProblemNameAnalyzer(), """
			public sealed class MyController : ControllerBase
			{
				[ProducesProblems<MyProblems>(nameof(MyProblems.Boom))]
				public IActionResult Get() => this.Ok();

				[ProducesProblems<MyProblems>]
				public IActionResult All() => this.Ok();
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(0);
	}

	[Test]
	public async Task ProblemCreatedOnceAndShared_IsReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ProblemDeclarationAnalyzer(), """
			public sealed class CachedProblems : ProblemProducerBase
			{
				public static ExplainedProblemDetails Property { get; } = new() { Type = "/problems/one" };
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD003");
	}

	[Test]
	public async Task ProblemDeclaredAsAField_IsReportedAsNotBeingOne()
	{
		// Only properties are read, so a field is not a shared problem - it is one that does not exist.
		List<Diagnostic> reported = await AnalyzerTests.Run(new ProblemDeclarationAnalyzer(), """
			public sealed class FieldProblems : ProblemProducerBase
			{
				public static readonly ExplainedProblemDetails Field = new() { Type = "/problems/two" };
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD005");
	}

	[Test]
	public async Task ProducerNestedInAGenericType_IsReportedToo()
	{
		// Its own arity is zero, but the container still cannot create it.
		List<Diagnostic> reported = await AnalyzerTests.Run(new ProblemDeclarationAnalyzer(), """
			public sealed class Outer<TAnything>
			{
				public sealed class NestedProblems : ProblemProducerBase
				{
					public static ExplainedProblemDetails Nested => new() { Type = "/problems/nested" };
				}
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD004");
	}

	[Test]
	public async Task GenericProducer_IsReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ProblemDeclarationAnalyzer(), """
			public sealed class GenericProblems<TAnything> : ProblemProducerBase
			{
				public static ExplainedProblemDetails Boom2 => new() { Type = "/problems/generic" };
			}
			""");

		await Assert.That(reported.Count).IsEqualTo(1);
		await Assert.That(reported[0].Id).IsEqualTo("CHPD004");
	}

	[Test]
	public async Task ProducerWrittenTheWayItShouldBe_IsNotReported()
	{
		List<Diagnostic> reported = await AnalyzerTests.Run(new ProblemDeclarationAnalyzer(), "");

		await Assert.That(reported.Count).IsEqualTo(0);
	}

	private static Task<List<Diagnostic>> Run(
		Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer analyzer, string source)
	{
		return AnalyzerHarness.RunAsync(analyzer, AnalyzerTests.Producer + Environment.NewLine + source);
	}
}
