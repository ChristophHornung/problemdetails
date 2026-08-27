namespace Chorn.AspNetCore.ProblemDetails.OpenApi;

using Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Adds the explained problems to the built-in open api document.
/// </summary>
public static class ExplainedProblemsOpenApiExtensions
{
	/// <summary>
	/// Documents the problems every endpoint declares via <see cref="ProducesProblemsAttribute{TProducer}"/>.
	/// </summary>
	/// <param name="options">The open api options.</param>
	/// <returns>The options, to chain calls on.</returns>
	/// <example>
	/// <code>
	/// builder.Services.AddOpenApi(options => options.AddExplainedProblems());
	/// </code>
	/// </example>
	public static OpenApiOptions AddExplainedProblems(this OpenApiOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		options.AddOperationTransformer<ProblemResponsesOperationTransformer>();
		return options;
	}
}
