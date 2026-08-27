namespace Chorn.AspNetCore.ProblemDetails.Swashbuckle;

using Microsoft.Extensions.DependencyInjection;
using global::Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Adds the explained problems to a Swashbuckle document.
/// </summary>
public static class ExplainedProblemsSwaggerGenExtensions
{
	/// <summary>
	/// Documents the problems every endpoint declares via <see cref="ProducesProblemsAttribute{TProducer}"/>.
	/// </summary>
	/// <param name="options">The swagger generation options.</param>
	/// <returns>The options, to chain calls on.</returns>
	/// <example>
	/// <code>
	/// builder.Services.AddSwaggerGen(options => options.AddExplainedProblems());
	/// </code>
	/// </example>
	public static SwaggerGenOptions AddExplainedProblems(this SwaggerGenOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		options.OperationFilter<ProblemResponsesOperationFilter>();
		return options;
	}
}
