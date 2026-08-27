namespace Chorn.AspNetCore.ProblemDetails.Tests.App;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Stands in for the web project - the assembly the host is named after.
/// </summary>
public sealed class AppProblems : ProblemProducerBase
{
	/// <summary>
	/// Gets the problem the application itself declares.
	/// </summary>
	public static ExplainedProblemDetails FromTheApp => new()
	{
		Title = "A problem from the application",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/app/one",
		Explanation = "Declared in the web project itself."
	};
}
