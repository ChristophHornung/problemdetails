namespace Chorn.AspNetCore.ProblemDetails;

using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

/// <summary>
/// Writes the problems an endpoint declares into its open api operation.
/// </summary>
/// <remarks>
/// Compiled into both open api integrations - they differ only in where the operation and the problem details
/// schema come from, not in what the document ends up saying.
/// </remarks>
internal static class ProblemResponseDocumentation
{
	/// <summary>
	/// Gets the problems the endpoint declares via <see cref="ProducesProblemsAttribute{TProducer}"/>.
	/// </summary>
	/// <param name="description">The endpoint being documented.</param>
	/// <returns>The documented problems, one per problem type.</returns>
	public static List<DocumentedProblem> GetProblems(ApiDescription description)
	{
		return description.ActionDescriptor.EndpointMetadata
			.OfType<IProducesProblems>()
			.SelectMany(metadata => metadata.GetProblems())
			.DistinctBy(problem => problem.Details.Type)
			.ToList();
	}

	/// <summary>
	/// Adds every problem as an example of the <c>application/problem+json</c> response for its status code.
	/// </summary>
	/// <param name="operation">The operation to document.</param>
	/// <param name="problems">The problems the endpoint can answer with.</param>
	/// <param name="schema">The problem details schema, referenced by every response added here.</param>
	public static void Apply(OpenApiOperation operation, IEnumerable<DocumentedProblem> problems,
		IOpenApiSchema schema)
	{
		operation.Responses ??= new OpenApiResponses();

		List<DocumentedProblem> documented = problems.ToList();

		// The example key is the declared name, qualified with the producer only when two producers share it -
		// both may well declare a NotFound.
		HashSet<string> ambiguous = documented.GroupBy(problem => problem.Name, StringComparer.Ordinal)
			.Where(sameName => sameName.Count() > 1)
			.Select(sameName => sameName.Key)
			.ToHashSet(StringComparer.Ordinal);

		foreach (IGrouping<int, DocumentedProblem> problemsPerStatus in documented.GroupBy(problem =>
					 problem.Details.Status ?? StatusCodes.Status500InternalServerError))
		{
			OpenApiMediaType mediaType =
				ProblemResponseDocumentation.GetProblemContent(operation.Responses, problemsPerStatus.Key, schema);

			mediaType.Examples ??= new Dictionary<string, IOpenApiExample>();
			foreach (DocumentedProblem problem in problemsPerStatus)
			{
				string key = ambiguous.Contains(problem.Name)
					? problem.Producer.Name + "." + problem.Name
					: problem.Name;

				mediaType.Examples[key] = new OpenApiExample
				{
					Summary = problem.Details.Type,
					Description = problem.Details.Explanation,
					Value = problem.Details.ToResponseJson()
				};
			}
		}

		ProblemResponseDocumentation.SortByStatusCode(operation.Responses);
	}

	/// <summary>
	/// Restores the ascending status code order, as a response added here would otherwise trail the ones the
	/// endpoint declares itself - including the <c>default</c> one.
	/// </summary>
	private static void SortByStatusCode(OpenApiResponses responses)
	{
		// A Dictionary keeps insertion order as long as nothing is removed, which holds once it is cleared.
		List<KeyValuePair<string, IOpenApiResponse>> sorted = responses
			.OrderBy(response =>
				int.TryParse(response.Key, CultureInfo.InvariantCulture, out int status) ? status : int.MaxValue)
			.ToList();

		responses.Clear();
		foreach (KeyValuePair<string, IOpenApiResponse> response in sorted)
		{
			responses.Add(response.Key, response.Value);
		}
	}

	/// <summary>
	/// Gets the <c>application/problem+json</c> content of the response for the given status code, adding the
	/// response or the content if the endpoint does not declare them itself.
	/// </summary>
	private static OpenApiMediaType GetProblemContent(OpenApiResponses responses, int statusCode,
		IOpenApiSchema schema)
	{
		string key = statusCode.ToString(CultureInfo.InvariantCulture);

		// A response documented by ProducesResponseType is kept as it is - it carries the description taken from
		// the <response> documentation, which says more about this endpoint than a status code ever could.
		if (!responses.TryGetValue(key, out IOpenApiResponse? existing) || existing is not OpenApiResponse response)
		{
			response = new OpenApiResponse { Description = ReasonPhrases.GetReasonPhrase(statusCode) };
			responses[key] = response;
		}

		response.Content ??= new Dictionary<string, OpenApiMediaType>();
		if (!response.Content.TryGetValue(MediaTypeNames.Application.ProblemJson, out OpenApiMediaType? mediaType))
		{
			mediaType = new OpenApiMediaType();
			response.Content[MediaTypeNames.Application.ProblemJson] = mediaType;
		}

		mediaType.Schema ??= schema;
		return mediaType;
	}
}
