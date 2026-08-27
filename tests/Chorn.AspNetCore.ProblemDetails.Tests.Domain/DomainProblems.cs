namespace Chorn.AspNetCore.ProblemDetails.Tests.Domain;

using Microsoft.AspNetCore.Http;

/// <summary>
/// A producer in a domain library, the way a controller can live in a class library.
/// </summary>
/// <remarks>
/// Nothing registers this assembly. It is found because the application references it and it uses this
/// package - the same rule that gets a controller in a class library routed to.
/// </remarks>
public sealed class DomainProblems : ProblemProducerBase
{
	/// <summary>
	/// Gets the problem this library can answer with.
	/// </summary>
	public static ExplainedProblemDetails FromTheDomain => new()
	{
		Title = "A problem from a referenced library",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/domain/one",
		Explanation = "Declared next to the code that throws it, rather than in the web project."
	};
}
