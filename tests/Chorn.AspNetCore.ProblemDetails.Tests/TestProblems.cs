namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Microsoft.AspNetCore.Http;

/// <summary>
/// The producer the unit tests read from.
/// </summary>
internal sealed class TestProblems : ProblemProducerBase
{
	internal static ExplainedProblemDetails InternalProblem => new()
	{
		Title = "An internal problem",
		Detail = "Declared by an internal property.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/test/internal",
		Explanation = "Producers usually declare their problems as internal, so this has to be found."
	};

	/// <summary>
	/// Gets a problem declared by a public property, to prove the visibility does not matter.
	/// </summary>
	public static ExplainedProblemDetails PublicProblem => new()
	{
		Title = "A public problem",
		Detail = "Declared by a public property.",
		Status = StatusCodes.Status403Forbidden,
		Type = "/problems/test/public",
		Explanation = "Public properties are found as well."
	};

	/// <summary>
	/// Gets something that is not a problem, to prove it is skipped.
	/// </summary>
	internal static string NotAProblem => "This property is not a problem and has to be ignored.";

	private static ExplainedProblemDetails PrivateProblem => new()
	{
		Title = "A private problem",
		Detail = "Declared by a private property.",
		Status = StatusCodes.Status500InternalServerError,
		Type = "/problems/test/private",
		Explanation = "Private properties are found as well."
	};
}

/// <summary>
/// A second producer, to prove that the storage merges them.
/// </summary>
internal sealed class OtherTestProblems : ProblemProducerBase
{
	internal static ExplainedProblemDetails OtherProblem => new()
	{
		Title = "Another problem",
		Detail = "Declared by another producer.",
		Status = StatusCodes.Status404NotFound,
		Type = "/problems/test/other",
		Explanation = "Every producer contributes its problems to the storage."
	};
}

/// <summary>
/// A base producer holding the problems a group of producers share. Abstract, so it is never registered itself.
/// </summary>
internal abstract class SharedTestProblems : ProblemProducerBase
{
	internal static ExplainedProblemDetails InheritedProblem => new()
	{
		Title = "An inherited problem",
		Detail = "Declared on a base producer.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/test/inherited",
		Explanation = "A producer hierarchy is a reasonable way to share problems."
	};
}

/// <summary>
/// A producer that inherits problems from its base as well as declaring its own.
/// </summary>
internal sealed class DerivedTestProblems : SharedTestProblems
{
	internal static ExplainedProblemDetails OwnProblem => new()
	{
		Title = "A derived problem",
		Detail = "Declared on the derived producer.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/test/derived",
		Explanation = "The derived producer contributes both its own and the inherited problems."
	};
}

/// <summary>
/// An open generic producer, which the container could never create and the scan therefore has to skip.
/// </summary>
/// <typeparam name="TAnything">Whatever makes it generic.</typeparam>
internal sealed class GenericTestProblems<TAnything> : ProblemProducerBase
{
	internal static ExplainedProblemDetails NeverRegistered => new()
	{
		Title = "Never registered",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/test/generic"
	};
}

/// <summary>
/// Documents a problem that <see cref="TestProblems"/> does not declare, which is what the startup validation
/// has to catch - on a private method, which it also has to look at.
/// </summary>
/// <remarks>
/// This is the reason the other tests turn the validation off: it is deliberately broken, and it lives in this
/// assembly, which is the one they scan.
/// </remarks>
internal sealed class BrokenlyDocumented
{
	[ProducesProblems<TestProblems>("InternalProblemMisspelled")]
	private static void NotEvenAnEndpoint()
	{
	}
}
