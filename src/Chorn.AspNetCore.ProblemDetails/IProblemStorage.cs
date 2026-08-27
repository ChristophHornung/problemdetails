namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// Answers for the problem type a caller received.
/// </summary>
/// <remarks>
/// The seam behind <see cref="ProblemExplanationEndpointRouteBuilderExtensions.MapProblemExplanations"/>, so an
/// application can decorate the lookup - filter by audience, merge a second catalogue, cache - without giving up
/// the endpoint. <see cref="ProblemStorage"/> is the implementation registered by default.
/// </remarks>
public interface IProblemStorage
{
	/// <summary>
	/// Gets every problem the application can produce.
	/// </summary>
	IReadOnlyCollection<ExplainedProblemDetails> AllProblems { get; }

	/// <summary>
	/// Gets the problem details for the given type.
	/// </summary>
	/// <param name="type">The type of problem.</param>
	/// <returns>The problem detail for the problem or <c>null</c> if the problem is unknown.</returns>
	ExplainedProblemDetails? GetProblemDetails(string type);
}
