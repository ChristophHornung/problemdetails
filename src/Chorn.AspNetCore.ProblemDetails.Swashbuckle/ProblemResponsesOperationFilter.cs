namespace Chorn.AspNetCore.ProblemDetails.Swashbuckle;

using Microsoft.OpenApi;
using global::Swashbuckle.AspNetCore.SwaggerGen;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Documents the problems an endpoint declares via <see cref="ProducesProblemsAttribute{TProducer}"/>.
/// </summary>
/// <remarks>
/// Every problem becomes an example of the <c>application/problem+json</c> response for its status code: keyed by
/// the name it is declared under, summarised by its type, and described by its
/// <see cref="ExplainedProblemDetails.Explanation"/> - which is written for exactly the reader of an api document.
/// </remarks>
public class ProblemResponsesOperationFilter : IOperationFilter
{
	/// <inheritdoc />
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(context);

		List<DocumentedProblem> problems = ProblemResponseDocumentation.GetProblems(context.ApiDescription);

		if (problems.Count == 0)
		{
			return;
		}

		// Generating the schema registers ProblemDetails in the document components, so the responses can
		// reference it instead of repeating it.
		IOpenApiSchema schema =
			context.SchemaGenerator.GenerateSchema(typeof(MvcProblemDetails), context.SchemaRepository);

		ProblemResponseDocumentation.Apply(operation, problems, schema);
	}
}
