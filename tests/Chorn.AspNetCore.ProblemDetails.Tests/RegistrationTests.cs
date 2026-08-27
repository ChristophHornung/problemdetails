namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies what a single AddExplainedProblemDetails puts into the container.
/// </summary>
/// <remarks>
/// The tests that are not about discovery name the assembly, because this one holds fixtures that are broken on
/// purpose. The ones that are about discovery stand a fake host up in front of
/// <c>Chorn.AspNetCore.ProblemDetails.Tests.App</c>, which references
/// <c>Chorn.AspNetCore.ProblemDetails.Tests.Domain</c> - an application and a domain library.
/// </remarks>
public class RegistrationTests
{
	private const string Application = "Chorn.AspNetCore.ProblemDetails.Tests.App";

	[Test]
	public async Task Registration_ScansTheApplicationTheHostIsNamedAfter()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterFor(RegistrationTests.Application);

		ProblemStorage storage = provider.GetRequiredService<ProblemStorage>();

		await Assert.That(storage.GetProblemDetails("/problems/app/one")).IsNotNull();

		// Not the assembly the call is made from - that is this one, and its producers are not the app's.
		await Assert.That(storage.GetProblemDetails("/problems/test/internal")).IsNull();
	}

	[Test]
	public async Task Registration_AlsoScansReferencedLibrariesThatUseThePackage()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterFor(RegistrationTests.Application);

		// Nobody registered the domain library. It is found because the application references it and it uses
		// this package - the same rule that gets a controller in a class library routed to.
		await Assert.That(provider.GetRequiredService<ProblemStorage>()
			.GetProblemDetails("/problems/domain/one")).IsNotNull();
	}

	[Test]
	public async Task Registration_ScansNothingBeyondThat()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterFor(RegistrationTests.Application);

		await Assert.That(provider.GetRequiredService<ProblemStorage>().AllProblems.Count).IsEqualTo(2);
	}

	[Test]
	public async Task Registration_FindsTheProblemsAProducerInherits()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterHere();

		ProblemStorage storage = provider.GetRequiredService<ProblemStorage>();

		// The base producer is abstract and never registered, so its problems only exist through the derived one.
		await Assert.That(storage.GetProblemDetails("/problems/test/inherited")).IsNotNull();
		await Assert.That(storage.GetProblemDetails("/problems/test/derived")).IsNotNull();
	}

	[Test]
	public async Task Registration_SkipsAnOpenGenericProducer()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterHere();

		// Registering it would satisfy every other check and then fail the container at the first resolve.
		await Assert.That(provider.GetRequiredService<ProblemStorage>()
			.GetProblemDetails("/problems/test/generic")).IsNull();
		await Assert.That(provider.GetServices<IProblemProducer>().Count()).IsEqualTo(3);
	}

	[Test]
	public async Task Registration_TakesTheProducersItIsGivenInstead()
	{
		ServiceCollection services = new();
		services.AddExplainedProblemDetails(options =>
		{
			options.ValidateDocumentedProblems = false;
			options.AddProducer<TestProblems>();
		});

		await using ServiceProvider provider = services.BuildServiceProvider();
		ProblemStorage storage = provider.GetRequiredService<ProblemStorage>();

		await Assert.That(storage.AllProblems.Count).IsEqualTo(3);
		await Assert.That(storage.GetProblemDetails("/problems/test/other")).IsNull();
	}

	[Test]
	public async Task Registration_ExposesTheStorageThroughItsInterface()
	{
		await using ServiceProvider provider = RegistrationTests.RegisterHere();

		await Assert.That(object.ReferenceEquals(provider.GetRequiredService<IProblemStorage>(),
			provider.GetRequiredService<ProblemStorage>())).IsTrue();
	}

	[Test]
	public async Task Registration_RegistersTheExceptionHandler()
	{
		ServiceCollection services = new();
		RegistrationTests.ScanHere(services);

		await Assert.That(services.Any(service => service.ServiceType == typeof(IExceptionHandler))).IsTrue();
	}

	[Test]
	public async Task Registration_LeavesTheExceptionHandlerOutWhenAsked()
	{
		ServiceCollection services = new();
		services.AddExplainedProblemDetails(options =>
		{
			options.ValidateDocumentedProblems = false;
			options.RegisterExceptionHandler = false;
			options.ScanAssemblies(typeof(TestProblems).Assembly);
		});

		await Assert.That(services.Any(service => service.ServiceType == typeof(IExceptionHandler))).IsFalse();
	}

	[Test]
	public async Task RegisteringTwice_DoesNotDuplicateAnything()
	{
		ServiceCollection services = new();
		services.AddLogging();
		RegistrationTests.ScanHere(services);
		RegistrationTests.ScanHere(services);

		await using ServiceProvider provider = services.BuildServiceProvider();

		await Assert.That(provider.GetServices<IProblemProducer>().Count()).IsEqualTo(3);
		await Assert.That(provider.GetServices<IExceptionHandler>().Count()).IsEqualTo(1);
		await Assert.That(provider.GetRequiredService<ProblemStorage>().AllProblems.Count).IsEqualTo(6);
	}

	/// <summary>
	/// Registers as an application of the given name would.
	/// </summary>
	private static ServiceProvider RegisterFor(string applicationName)
	{
		ServiceCollection services = new();
		FakeHost.AddNamed(services, applicationName);
		services.AddExplainedProblemDetails();
		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Registers the producers of this assembly, naming it rather than letting the discovery find it.
	/// </summary>
	private static ServiceProvider RegisterHere()
	{
		ServiceCollection services = new();
		RegistrationTests.ScanHere(services);
		return services.BuildServiceProvider();
	}

	private static void ScanHere(IServiceCollection services)
	{
		services.AddExplainedProblemDetails(options =>
		{
			options.ValidateDocumentedProblems = false;
			options.ScanAssemblies(Assembly.GetExecutingAssembly());
		});
	}
}
