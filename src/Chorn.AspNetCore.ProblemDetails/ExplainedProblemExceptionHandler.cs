namespace Chorn.AspNetCore.ProblemDetails;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles <see cref="ExplainedProblemException"/> and converts it into a standard problem details response.
/// </summary>
/// <param name="logger">The logger to record the handled problem with.</param>
/// <remarks>
/// Registered by
/// <see cref="ExplainedProblemDetailsServiceCollectionExtensions.AddExplainedProblemDetails"/>. Every other
/// exception is left to the handlers after it - returning <c>false</c> is what passes it on.
/// </remarks>
public class ExplainedProblemExceptionHandler(ILogger<ExplainedProblemExceptionHandler> logger) : IExceptionHandler
{
	/// <inheritdoc />
	public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
		CancellationToken cancellationToken)
	{
		if (exception is not ExplainedProblemException explainedProblemException)
		{
			return false;
		}

		ExplainedProblemDetails problemDetails = explainedProblemException.ProblemDetails;

		logger.LogDebug(exception, "Answering with the problem {ProblemType} ({ProblemStatus}).",
			problemDetails.Type, problemDetails.Status);

		httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

		IResult response = Results.Problem(
			detail: problemDetails.Detail,
			instance: problemDetails.Instance,
			statusCode: problemDetails.Status,
			title: problemDetails.Title,
			type: problemDetails.Type,
			extensions: problemDetails.Extensions);

		await response.ExecuteAsync(httpContext);
		return true;
	}
}
