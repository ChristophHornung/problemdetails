namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verifies that the handler answers for its own exception and nothing else.
/// </summary>
public class ExceptionHandlerTests
{
	[Test]
	public async Task ExplainedProblem_IsHandled()
	{
		DefaultHttpContext context = ExceptionHandlerTests.Context();

		bool handled = await ExceptionHandlerTests.Handler()
			.TryHandleAsync(context, TestProblems.InternalProblem.AsException(), CancellationToken.None);

		await Assert.That(handled).IsTrue();
		await Assert.That(context.Response.StatusCode).IsEqualTo(409);
	}

	[Test]
	public async Task AnyOtherException_IsPassedOn()
	{
		DefaultHttpContext context = ExceptionHandlerTests.Context();

		// Returning false is what leaves the exception to the handlers after this one. Swallowing it would turn
		// every unrelated failure in the application into an empty 500.
		bool handled = await ExceptionHandlerTests.Handler()
			.TryHandleAsync(context, new InvalidOperationException("unrelated"), CancellationToken.None);

		await Assert.That(handled).IsFalse();
		await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
	}

	[Test]
	public async Task ProblemWithoutAStatus_IsAnInternalServerError()
	{
		DefaultHttpContext context = ExceptionHandlerTests.Context();
		ExplainedProblemDetails problem = new() { Title = "No status at all" };

		await ExceptionHandlerTests.Handler()
			.TryHandleAsync(context, problem.AsException(), CancellationToken.None);

		await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
	}

	private static ExplainedProblemExceptionHandler Handler()
	{
		return new ExplainedProblemExceptionHandler(NullLogger<ExplainedProblemExceptionHandler>.Instance);
	}

	/// <summary>
	/// Builds a request the handler can write a problem response into.
	/// </summary>
	/// <returns>A context with the services writing a problem response needs.</returns>
	private static DefaultHttpContext Context()
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddProblemDetails();

		return new DefaultHttpContext
		{
			RequestServices = services.BuildServiceProvider(),
			Response = { Body = new MemoryStream() }
		};
	}
}
