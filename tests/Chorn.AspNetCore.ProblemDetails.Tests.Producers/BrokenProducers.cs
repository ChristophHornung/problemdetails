namespace Chorn.AspNetCore.ProblemDetails.Tests.Producers;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Producers that are deliberately broken.
/// </summary>
/// <remarks>
/// They live in their own assembly because a broken producer breaks every scan of the assembly holding it -
/// which is the point, and which would take the rest of the test suite with it if they sat next to the tests.
/// Nothing scans this assembly unless a test names it.
/// </remarks>
public sealed class ThrowingProducer : ProblemProducerBase
{
	/// <summary>
	/// Gets a problem that cannot be read.
	/// </summary>
	public static ExplainedProblemDetails Unreadable =>
		throw new InvalidOperationException("The declaration itself is broken.");
}

/// <summary>
/// Declares two problems that share a type, which a single producer must not do.
/// </summary>
public sealed class DuplicateWithinProducer : ProblemProducerBase
{
	/// <summary>
	/// Gets the first problem.
	/// </summary>
	public static ExplainedProblemDetails First => new()
	{
		Title = "The first",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/duplicate"
	};

	/// <summary>
	/// Gets a second problem that claims the same type.
	/// </summary>
	public static ExplainedProblemDetails Second => new()
	{
		Title = "The second",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/duplicate"
	};
}

/// <summary>
/// Claims a problem type that a producer in the test assembly already declares.
/// </summary>
public sealed class ClashingProducer : ProblemProducerBase
{
	/// <summary>
	/// Gets a problem whose type belongs to another producer.
	/// </summary>
	public static ExplainedProblemDetails Clash => new()
	{
		Title = "A clash",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/test/internal"
	};
}

/// <summary>
/// Declares a problem that is not there.
/// </summary>
public sealed class NullProducer : ProblemProducerBase
{
	/// <summary>
	/// Gets nothing at all.
	/// </summary>
	public static ExplainedProblemDetails? Missing => null;
}
