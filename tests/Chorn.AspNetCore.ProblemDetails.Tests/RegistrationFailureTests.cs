namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Tests.Producers;
using Chorn.AspNetCore.ProblemDetails.Tests.Startup;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that the mistakes a registration can make are reported when the application starts, rather than
/// when the first request happens to need the part that was wrong.
/// </summary>
public class RegistrationFailureTests
{
	[Test]
	public async Task RegistrationFromASharedStartupLibrary_StillFindsTheApplication()
	{
		// A ServiceDefaults-style helper registers on the application's behalf. What matters is the application
		// the host is named after, not the assembly the call happens to sit in - which here is one that declares
		// no producer at all.
		ServiceCollection services = new();
		FakeHost.AddNamed(services, "Chorn.AspNetCore.ProblemDetails.Tests.App");
		services.AddDefaults();

		await using ServiceProvider provider = services.BuildServiceProvider();

		await Assert.That(provider.GetRequiredService<ProblemStorage>()
			.GetProblemDetails("/problems/app/one")).IsNotNull();
	}

	[Test]
	public async Task ApplicationWithNoProducersAnywhere_IsReported()
	{
		// The startup library declares none and references none that do, so there is nothing to register and
		// something is wrong with the setup.
		ServiceCollection services = new();
		FakeHost.AddNamed(services, "Chorn.AspNetCore.ProblemDetails.Tests.Startup");

		InvalidOperationException? failure =
			RegistrationFailureTests.FailureOf(() => services.AddExplainedProblemDetails());

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("Chorn.AspNetCore.ProblemDetails.Tests.Startup");
		await Assert.That(failure.Message).Contains("ScanAssemblyOf");
	}

	[Test]
	public async Task ProblemNamedByDocumentationThatDoesNotExist_FailsTheStartup()
	{
		// The validation walks this assembly and finds BrokenlyDocumented - on a private method, which it has
		// to look at as well.
		InvalidOperationException? failure = RegistrationFailureTests.FailureOf(
			() => new ServiceCollection().AddExplainedProblemDetails(
				options => options.ScanAssemblies(typeof(TestProblems).Assembly)));

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("InternalProblemMisspelled");
		await Assert.That(failure.Message).Contains(nameof(TestProblems));
	}

	[Test]
	public async Task ProducerWhoseProblemCannotBeRead_FailsTheStartup()
	{
		InvalidOperationException? failure = RegistrationFailureTests.FailureOf(() =>
			new ServiceCollection().AddExplainedProblemDetails(
				options => options.AddProducer<ThrowingProducer>()));

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains(nameof(ThrowingProducer));
		await Assert.That(failure.Message).Contains(nameof(ThrowingProducer.Unreadable));
		await Assert.That(failure.InnerException).IsNotNull();
	}

	[Test]
	public async Task TwoProducersDeclaringTheSameType_FailTheStartup()
	{
		InvalidOperationException? failure = RegistrationFailureTests.FailureOf(() =>
			new ServiceCollection().AddExplainedProblemDetails(options =>
			{
				options.AddProducer<TestProblems>();
				options.AddProducer<ClashingProducer>();
			}));

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("/problems/test/internal");
	}

	[Test]
	public async Task UnrelatedType_CannotBeRegisteredAsAProducer()
	{
		ExplainedProblemDetailsOptions options = new();

		ArgumentException? failure = null;
		try
		{
			options.AddProducer(typeof(string));
		}
		catch (ArgumentException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains(nameof(IProblemProducer));
	}

	[Test]
	public async Task OpenGenericProducer_CannotBeRegisteredEither()
	{
		ExplainedProblemDetailsOptions options = new();

		ArgumentException? failure = null;
		try
		{
			options.AddProducer(typeof(GenericTestProblems<>));
		}
		catch (ArgumentException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains(nameof(IProblemProducer));
	}

	private static InvalidOperationException? FailureOf(Action registration)
	{
		try
		{
			registration();
			return null;
		}
		catch (InvalidOperationException exception)
		{
			return exception;
		}
	}
}
