namespace Chorn.AspNetCore.ProblemDetails.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Documents the problems an endpoint declares via <see cref="ProducesProblemsAttribute{TProducer}"/>.
/// </summary>
/// <remarks>
/// Every problem becomes an example of the <c>application/problem+json</c> response for its status code: keyed by
/// the name it is declared under, summarised by its type, and described by its
/// <see cref="ExplainedProblemDetails.Explanation"/> - which is written for exactly the reader of an api document.
/// </remarks>
public sealed class ProblemResponsesOperationTransformer : IOpenApiOperationTransformer
{
	private const string SchemaId = "ProblemDetails";

	/// <inheritdoc />
	public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(context);

		List<DocumentedProblem> problems = ProblemResponseDocumentation.GetProblems(context.Description);

		if (problems.Count == 0)
		{
			return;
		}

		IOpenApiSchema schema = await context
			.GetOrCreateSchemaAsync(typeof(MvcProblemDetails), cancellationToken: cancellationToken);

		ProblemResponseDocumentation.Apply(operation, problems,
			ProblemResponsesOperationTransformer.AsComponent(context.Document, schema));
	}

	/// <summary>
	/// Registers the problem details schema in the document components and returns a reference to it.
	/// </summary>
	/// <param name="document">The document being generated, if the generation exposes it.</param>
	/// <param name="schema">The problem details schema.</param>
	/// <returns>The reference to put on the responses, or the schema itself if there is no document.</returns>
	/// <remarks>
	/// Written out once rather than repeated on every problem response of every endpoint.
	/// </remarks>
	private static IOpenApiSchema AsComponent(OpenApiDocument? document, IOpenApiSchema schema)
	{
		if (document == null)
		{
			return schema;
		}

		if (document.Components?.Schemas?.ContainsKey(ProblemResponsesOperationTransformer.SchemaId) != true)
		{
			document.AddComponent(ProblemResponsesOperationTransformer.SchemaId, schema);
		}

		return new OpenApiSchemaReference(ProblemResponsesOperationTransformer.SchemaId, document);
	}
}
