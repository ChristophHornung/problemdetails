namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Tests.Producers;

/// <summary>
/// Verifies that a problem exists everywhere as soon as it is declared as a static property on a producer.
/// </summary>
public class ProblemDeclarationTests
{
	[Test]
	public async Task DeclaredProblems_AreFoundRegardlessOfVisibility()
	{
		List<DocumentedProblem> problems = ProblemProducerReflection.GetProblems(typeof(TestProblems)).ToList();

		await Assert.That(problems.Count).IsEqualTo(3);
		await Assert.That(problems.Any(problem => problem.Name == "InternalProblem")).IsTrue();
		await Assert.That(problems.Any(problem => problem.Name == "PublicProblem")).IsTrue();
		await Assert.That(problems.Any(problem => problem.Name == "PrivateProblem")).IsTrue();
	}

	[Test]
	public async Task PropertyThatIsNotAProblem_IsSkipped()
	{
		List<DocumentedProblem> problems = ProblemProducerReflection.GetProblems(typeof(TestProblems)).ToList();

		await Assert.That(problems.Any(problem => problem.Name == "NotAProblem")).IsFalse();
	}

	[Test]
	public async Task ProblemsOfABaseProducer_AreFoundOnTheDerivedOne()
	{
		// Reflection does not return inherited static members, so the hierarchy has to be walked by hand.
		List<DocumentedProblem> problems =
			ProblemProducerReflection.GetProblems(typeof(DerivedTestProblems)).ToList();

		await Assert.That(problems.Count).IsEqualTo(2);
		await Assert.That(problems.Any(problem => problem.Name == "InheritedProblem")).IsTrue();
		await Assert.That(problems.Any(problem => problem.Name == "OwnProblem")).IsTrue();
	}

	[Test]
	public async Task DeclaredProblem_KeepsTheNameAndTheProducerItIsDeclaredOn()
	{
		DocumentedProblem problem =
			ProblemProducerReflection.GetProblem(typeof(TestProblems), nameof(TestProblems.PublicProblem));

		await Assert.That(problem.Name).IsEqualTo(nameof(TestProblems.PublicProblem));
		await Assert.That(problem.Producer).IsEqualTo(typeof(TestProblems));
		await Assert.That(problem.Details.Type).IsEqualTo("/problems/test/public");
		await Assert.That(problem.Details.Status).IsEqualTo(403);
	}

	[Test]
	public async Task ProblemThatTheProducerDoesNotDeclare_ReportsTheOnesItDoes()
	{
		InvalidOperationException? failure = ProblemDeclarationTests.FailureOf(
			() => ProblemProducerReflection.GetProblem(typeof(TestProblems), "InternalProblemMisspelled"));

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("InternalProblem");
		await Assert.That(failure.Message).Contains(nameof(TestProblems));
	}

	[Test]
	public async Task ProblemThatThrows_NamesTheProperty()
	{
		InvalidOperationException? failure = ProblemDeclarationTests.FailureOf(
			() => ProblemProducerReflection.GetProblems(typeof(ThrowingProducer)).ToList());

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains(nameof(ThrowingProducer.Unreadable));
		await Assert.That(failure.InnerException).IsNotNull();
	}

	[Test]
	public async Task ProblemThatIsNull_NamesTheProperty()
	{
		InvalidOperationException? failure = ProblemDeclarationTests.FailureOf(
			() => ProblemProducerReflection.GetProblems(typeof(NullProducer)).ToList());

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains(nameof(NullProducer.Missing));
	}

	[Test]
	public async Task Producer_ExposesEveryProblemItDeclares()
	{
		IProblemProducer producer = new TestProblems();

		await Assert.That(producer.AllPossibleProblems.Count()).IsEqualTo(3);
	}

	[Test]
	public async Task DocumentationWithoutNames_DocumentsEveryProblemOfTheProducer()
	{
		IProducesProblems documentation = new ProducesProblemsAttribute<TestProblems>();

		await Assert.That(documentation.GetProblems().Count()).IsEqualTo(3);
	}

	[Test]
	public async Task DocumentationWithNames_DocumentsOnlyThose()
	{
		IProducesProblems documentation =
			new ProducesProblemsAttribute<TestProblems>(nameof(TestProblems.InternalProblem));

		List<DocumentedProblem> problems = documentation.GetProblems().ToList();

		await Assert.That(problems.Count).IsEqualTo(1);
		await Assert.That(problems[0].Details.Type).IsEqualTo("/problems/test/internal");
	}

	[Test]
	public async Task DocumentationOfAProblemThatDoesNotExist_FailsWhenItIsRead()
	{
		// The one place a name is written by hand rather than with nameof - it has to be one that cannot resolve.
		IProducesProblems documentation = new ProducesProblemsAttribute<TestProblems>("InternalProblemMisspelled");

		// The names resolve while enumerating, which is what the startup does to fail on a stale name.
		InvalidOperationException? failure =
			ProblemDeclarationTests.FailureOf(() => documentation.GetProblems().ToList());

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("InternalProblem");
	}

	private static InvalidOperationException? FailureOf(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (InvalidOperationException exception)
		{
			return exception;
		}
	}
}
