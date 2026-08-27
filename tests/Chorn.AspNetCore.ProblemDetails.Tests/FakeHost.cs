namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

/// <summary>
/// A host environment the registration can read the application name off, the way it reads a real one.
/// </summary>
/// <remarks>
/// The registration looks for the environment among the descriptors registered as an instance, which is how
/// mvc finds it too - so a plain singleton registration is enough to stand in for a host here.
/// </remarks>
internal sealed class FakeHost : IHostEnvironment
{
	/// <inheritdoc />
	public string ApplicationName { get; set; } = string.Empty;

	/// <inheritdoc />
	public string EnvironmentName { get; set; } = "Test";

	/// <inheritdoc />
	public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

	/// <inheritdoc />
	public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

	/// <summary>
	/// Adds a host named after the given assembly, so the registration treats it as the application.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="applicationName">The assembly name the host is named after.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddNamed(IServiceCollection services, string applicationName)
	{
		return services.AddSingleton<IHostEnvironment>(new FakeHost { ApplicationName = applicationName });
	}
}
