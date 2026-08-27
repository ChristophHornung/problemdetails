namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// A producer of problems.
/// </summary>
/// <remarks>
/// Implement it by deriving from <see cref="ProblemProducerBase"/> and declaring one static
/// <see cref="ExplainedProblemDetails"/> property per problem - that is all the wiring a problem needs.
/// </remarks>
public interface IProblemProducer
{
	/// <summary>
	/// Gets all possible problems this producer can generate.
	/// </summary>
	IEnumerable<ExplainedProblemDetails> AllPossibleProblems { get; }
}
