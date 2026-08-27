namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// Exception to throw from lower layers when a known explained problem should be returned to the client.
/// </summary>
/// <remarks>
/// <see cref="ExplainedProblemExceptionHandler"/> turns it into the problem response, so a service deep in the
/// call stack can answer with a documented problem without knowing about <c>HttpContext</c> or the controller
/// that started the request.
/// </remarks>
public class ExplainedProblemException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExplainedProblemException"/> class.
	/// </summary>
	/// <param name="problemDetails">The problem details to expose in the HTTP response.</param>
	/// <param name="innerException">Optional inner exception.</param>
	/// <exception cref="ArgumentNullException">if no problem is given.</exception>
	public ExplainedProblemException(ExplainedProblemDetails problemDetails, Exception? innerException = null)
		: base(ExplainedProblemException.MessageOf(problemDetails), innerException)
	{
		this.ProblemDetails = problemDetails;
	}

	/// <summary>
	/// Gets the explained problem details to return to the caller.
	/// </summary>
	public ExplainedProblemDetails ProblemDetails { get; }

	private static string? MessageOf(ExplainedProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);
		return problemDetails.Detail ?? problemDetails.Title;
	}
}
