namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Tests.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that nothing hands a caller an instance the catalogue is built from.
/// </summary>
/// <remarks>
/// A producer is a singleton and the storage keys on the very problems it exposes, so one caller adding a
/// per-request member to a problem it read would otherwise reach every later request.
/// </remarks>
public class SharedInstanceTests
{
	[Test]
	public async Task Producer_HandsOutACopy()
	{
		IProblemProducer producer = new TestProblems();

		foreach (ExplainedProblemDetails problem in producer.AllPossibleProblems)
		{
			problem.Title = "Rewritten";
			problem.Extensions["leaked"] = true;
		}

		await Assert.That(producer.AllPossibleProblems.Any(problem => problem.Title == "Rewritten")).IsFalse();
		await Assert.That(producer.AllPossibleProblems.Any(problem => problem.Extensions.ContainsKey("leaked")))
			.IsFalse();
	}

	[Test]
	public async Task ProducerResolvedFromTheContainer_CannotPoisonTheCatalogue()
	{
		ServiceCollection services = new();
		services.AddExplainedProblemDetails(options =>
		{
			options.ValidateDocumentedProblems = false;
			options.AddProducer<TestProblems>();
		});

		await using ServiceProvider provider = services.BuildServiceProvider();

		// Any consumer can resolve the producers - they are registered so the storage can be built from them.
		foreach (IProblemProducer producer in provider.GetServices<IProblemProducer>())
		{
			foreach (ExplainedProblemDetails problem in producer.AllPossibleProblems)
			{
				problem.Explanation = "poisoned";
			}
		}

		await Assert.That(provider.GetRequiredService<ProblemStorage>()
				.GetProblemDetails("/problems/test/internal")!.Explanation)
			.IsNotEqualTo("poisoned");
	}

	[Test]
	public async Task ResponseJson_CarriesTheMembersTheProblemDeclares()
	{
		ExplainedProblemDetails problem = new()
		{
			Title = "Declared",
			Status = StatusCodes.Status400BadRequest,
			Type = "/problems/declared",
			Extensions = { ["errorCode"] = "E42", ["retryable"] = false },
			Explanation = "Not for the wire."
		};

		System.Text.Json.Nodes.JsonObject json = problem.ToResponseJson();

		await Assert.That(json["errorCode"]!.GetValue<string>()).IsEqualTo("E42");
		await Assert.That(json["retryable"]!.GetValue<bool>()).IsFalse();
		await Assert.That(json.ContainsKey("explanation")).IsFalse();
	}

	[Test]
	public async Task DocumentationOfTheApplication_IsValidatedEvenWhenProducersAreNamed()
	{
		// The producer comes from another assembly, so only the application - this one, which declares
		// BrokenlyDocumented - can be the source of the failure.
		ServiceCollection services = new();
		FakeHost.AddNamed(services, "Chorn.AspNetCore.ProblemDetails.Tests");

		InvalidOperationException? failure = null;
		try
		{
			services.AddExplainedProblemDetails(options => options.AddProducer<DomainProblems>());
		}
		catch (InvalidOperationException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("InternalProblemMisspelled");
	}
}
