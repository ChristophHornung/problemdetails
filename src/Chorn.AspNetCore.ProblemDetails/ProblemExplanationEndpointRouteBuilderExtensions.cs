namespace Chorn.AspNetCore.ProblemDetails;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

/// <summary>
/// Maps the endpoints that answer for a problem type.
/// </summary>
public static class ProblemExplanationEndpointRouteBuilderExtensions
{
	/// <summary>
	/// The route the explanations are served under unless another one is given.
	/// </summary>
	public const string DefaultExplanationPattern = "/problem/{**problemType}";

	/// <summary>
	/// The route the deliberate failures are served under unless another one is given.
	/// </summary>
	public const string DefaultExpectedProblemPattern = "/problem/expected";

	/// <summary>
	/// The name of the route value carrying the problem type.
	/// </summary>
	private const string ProblemTypeRouteValue = "problemType";

	/// <summary>
	/// How much of an unknown problem type is repeated back, so an unbounded path cannot be echoed.
	/// </summary>
	private const int ReportedTypeLimit = 200;

	/// <summary>
	/// Maps the endpoint that hands a developer the explanation for the problem type they received.
	/// </summary>
	/// <param name="endpoints">The endpoint route builder.</param>
	/// <param name="pattern">The route to serve the explanations under. It has to capture the problem type as
	/// <c>{**problemType}</c> - a catch-all, because a type usually contains slashes.</param>
	/// <param name="problemTypePrefix">
	/// What the captured route value is missing to be the problem type, for types that do not start where the
	/// route does - e.g. <c>/problems/</c> for a route of <c>/problems/{**problemType}</c>.
	/// </param>
	/// <param name="endpointName">
	/// The endpoint name, for link generation. Unset by default: names are globally unique, and a fixed one would
	/// stop the endpoint being mapped twice.
	/// </param>
	/// <returns>The endpoint, to put conventions on.</returns>
	/// <exception cref="ArgumentException">if the pattern does not capture <c>problemType</c> as a catch-all.</exception>
	/// <remarks>
	/// Requesting the route without a type answers with every problem the application can produce. The endpoint
	/// is anonymous on purpose - a caller that just failed to authenticate is exactly who needs it; chain
	/// <c>.RequireAuthorization()</c> on the result to change that.
	/// </remarks>
	public static RouteHandlerBuilder MapProblemExplanations(this IEndpointRouteBuilder endpoints,
		[StringSyntax("Route")] string pattern = ProblemExplanationEndpointRouteBuilderExtensions
			.DefaultExplanationPattern,
		string? problemTypePrefix = null,
		string? endpointName = null)
	{
		ArgumentNullException.ThrowIfNull(endpoints);
		ArgumentNullException.ThrowIfNull(pattern);

		ProblemExplanationEndpointRouteBuilderExtensions.RequireCatchAllProblemType(pattern);

		RouteHandlerBuilder builder = endpoints.MapGet(pattern,
				(string? problemType, [FromServices] IProblemStorage storage) =>
					ProblemExplanationEndpointRouteBuilderExtensions.Explain(storage,
						ProblemExplanationEndpointRouteBuilderExtensions.AsProblemType(problemType,
							problemTypePrefix)))
			.WithSummary("Explains a problem type.")
			.WithTags("Problems")
			.AllowAnonymous();

		return endpointName == null ? builder : builder.WithName(endpointName);
	}

	/// <summary>
	/// Maps endpoints that never succeed, so a client developer can exercise their error handling against a
	/// real problem response.
	/// </summary>
	/// <param name="endpoints">The endpoint route builder.</param>
	/// <param name="pattern">The route prefix to serve the failures under.</param>
	/// <param name="problemTypeFormat">
	/// The problem type to answer with, <c>{0}</c> standing for the status code - e.g.
	/// <c>/problems/expected/{0}</c>. Unset, the responses carry no type: one that no producer declares could not
	/// be looked up.
	/// </param>
	/// <returns>The endpoint group, to put conventions on.</returns>
	/// <remarks>
	/// The route itself answers with a 500, and appending a status code between 400 and 599 answers with that
	/// one. Do not map this outside of a development environment.
	/// </remarks>
	public static RouteGroupBuilder MapExpectedProblems(this IEndpointRouteBuilder endpoints,
		[StringSyntax("Route")] string pattern = ProblemExplanationEndpointRouteBuilderExtensions
			.DefaultExpectedProblemPattern,
		string? problemTypeFormat = null)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		RouteGroupBuilder group = endpoints.MapGroup(pattern).WithTags("Problems").AllowAnonymous();

		group.MapGet("/",
				() => ProblemExplanationEndpointRouteBuilderExtensions.Expected(
					StatusCodes.Status500InternalServerError, problemTypeFormat))
			.WithSummary("Always answers with a problem.");

		group.MapGet("/{statusCode:int:range(400,599)}",
				(int statusCode) => ProblemExplanationEndpointRouteBuilderExtensions.Expected(statusCode,
					problemTypeFormat))
			.WithSummary("Always answers with a problem of the requested status.");

		return group;
	}

	/// <summary>
	/// Fails a route that does not capture the problem type the way the lookup needs it.
	/// </summary>
	/// <param name="pattern">The route to check.</param>
	/// <exception cref="ArgumentException">if there is no catch-all <c>problemType</c> parameter.</exception>
	/// <remarks>
	/// Parsed rather than searched for the name: <c>/problemType</c> contains it as literal text and would bind
	/// from the query string, and <c>/problem/{problemType}</c> captures one segment, which no type with a slash
	/// can match. Both would answer 404 for every real type without saying why.
	/// </remarks>
	private static void RequireCatchAllProblemType(string pattern)
	{
		RoutePattern parsed = RoutePatternFactory.Parse(pattern);

		if (parsed.GetParameter(ProblemExplanationEndpointRouteBuilderExtensions.ProblemTypeRouteValue) is
			{ IsCatchAll: true })
		{
			return;
		}

		throw new ArgumentException(
			"The route has to capture the problem type as a catch-all parameter, as in " +
			$"{ProblemExplanationEndpointRouteBuilderExtensions.DefaultExplanationPattern}.", nameof(pattern));
	}

	/// <summary>
	/// Turns the captured route value into the problem type to look up.
	/// </summary>
	/// <param name="captured">The captured route value, or <c>null</c> when the route carried none.</param>
	/// <param name="problemTypePrefix">What the route leaves out of the type.</param>
	/// <returns>The problem type, or <c>null</c> to ask for the whole catalogue.</returns>
	/// <remarks>
	/// Routing decodes a catch-all value except for <c>%2F</c>, so an encoded slash cannot become a segment
	/// separator. A problem type is not a route, so a slash in it is simply part of it.
	/// </remarks>
	private static string? AsProblemType(string? captured, string? problemTypePrefix)
	{
		return string.IsNullOrEmpty(captured)
			? null
			: problemTypePrefix + captured.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
	}

	private static Results<Ok<IReadOnlyCollection<ExplainedProblemDetails>>, Ok<ExplainedProblemDetails>,
		ProblemHttpResult> Explain(IProblemStorage storage, string? problemType)
	{
		if (problemType == null)
		{
			return TypedResults.Ok(storage.AllProblems);
		}

		ExplainedProblemDetails? details = storage.GetProblemDetails(problemType);

		if (details != null)
		{
			// The one place the explanation is the answer rather than a leak: this endpoint exists to hand it out.
#pragma warning disable CHPD001
			return TypedResults.Ok(details);
#pragma warning restore CHPD001
		}

		string reported = problemType.Length <= ProblemExplanationEndpointRouteBuilderExtensions.ReportedTypeLimit
			? problemType
			: problemType[..ProblemExplanationEndpointRouteBuilderExtensions.ReportedTypeLimit] + "...";

		return TypedResults.Problem(
			detail: $"No explanation is known for the problem type {reported}.",
			statusCode: StatusCodes.Status404NotFound,
			title: "Unknown problem type");
	}

	private static ProblemHttpResult Expected(int statusCode, string? problemTypeFormat)
	{
		return TypedResults.Problem(
			// The newlines are deliberate: a detail that spans lines is exactly what a client renderer has to
			// cope with, and this endpoint exists to be rendered.
			detail: "This endpoint always fails.\nIt is here so error handling can be pointed at a real\n" +
					"problem response.",
			statusCode: statusCode,
			title: $"Expected error {statusCode}",
			type: problemTypeFormat == null
				? null
				: string.Format(CultureInfo.InvariantCulture, problemTypeFormat, statusCode));
	}
}
