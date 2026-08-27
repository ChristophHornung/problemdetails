namespace Chorn.AspNetCore.ProblemDetails.Tests.Startup;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Stands in for the shared startup library every solution grows - a ServiceDefaults project, a company
/// extension method - that calls AddExplainedProblemDetails on the application's behalf.
/// </summary>
/// <remarks>
/// This assembly deliberately declares no producer of its own. Scanning it is exactly the mistake the
/// registration has to report rather than swallow.
/// </remarks>
public static class SharedStartup
{
	/// <summary>
	/// Registers the explained problem details the way a shared startup library would.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddDefaults(this IServiceCollection services)
	{
		return services.AddExplainedProblemDetails();
	}
}
