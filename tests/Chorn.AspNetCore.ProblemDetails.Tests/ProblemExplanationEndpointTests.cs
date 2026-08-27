namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies the endpoint that answers for the type a caller received.
/// </summary>
public class ProblemExplanationEndpointTests
{
	[Test]
	public async Task ProblemType_IsAnsweredWithItsExplanation()
	{
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync("/problems/user/not-signed-up");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(200);
		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
		await Assert.That(body.RootElement.GetProperty("explanation").GetString())
			.Contains("sign-up flow");
	}

	[Test]
	public async Task ProblemTypeWithAnEncodedSlash_IsFoundToo()
	{
		// Routing decodes a catch-all value except for %2F, which it leaves alone so an encoded slash cannot
		// become a segment separator. A problem type is not a route, so the slash is simply part of it.
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync("/problems/user%2Fnot-signed-up");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(200);
		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
	}

	[Test]
	public async Task UnknownProblemType_IsAnsweredWithAProblem()
	{
		using HttpResponseMessage response = await SampleApplication.Client.GetAsync("/problems/user/nothing");

		await Assert.That((int)response.StatusCode).IsEqualTo(404);
		await Assert.That(response.Content.Headers.ContentType!.MediaType)
			.IsEqualTo("application/problem+json");

		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo("Unknown problem type");
		await Assert.That(body.RootElement.GetProperty("detail").GetString())
			.Contains("/problems/user/nothing");
	}

	[Test]
	public async Task RouteWithoutAType_IsAnsweredWithEveryProblem()
	{
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync("/problems");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(200);

		List<JsonElement> problems = body.RootElement.EnumerateArray().ToList();

		await Assert.That(problems.Count).IsEqualTo(6);
		await Assert.That(problems.All(problem => problem.TryGetProperty("explanation", out JsonElement _)))
			.IsTrue();
		await Assert
			.That(problems.Any(problem =>
				problem.GetProperty("type").GetString() == "/problems/order/already-shipped")).IsTrue();
	}

	[Test]
	public async Task ExpectedProblem_AlwaysFails()
	{
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync("/problems/expected");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(500);
		await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo("Expected error 500");
	}

	[Test]
	public async Task ExpectedProblem_FailsWithTheRequestedStatus()
	{
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync("/problems/expected/418");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(418);
		await Assert.That(body.RootElement.GetProperty("status").GetInt32()).IsEqualTo(418);
		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/expected/418");
	}

	[Test]
	[Arguments("/problems")]
	[Arguments("/problemType")]
	[Arguments("/problems/{problemType}")]
	public async Task PatternThatDoesNotCaptureTheTypeAsACatchAll_IsRejected(string pattern)
	{
		// '/problemType' contains the name as literal text and would bind from the query string instead;
		// '{problemType}' captures a single segment, which no type containing a slash can ever match.
		await using WebApplication application = WebApplication.CreateBuilder().Build();

		ArgumentException? failure = null;
		try
		{
			application.MapProblemExplanations(pattern);
		}
		catch (ArgumentException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("problemType");
	}

	[Test]
	public async Task EndpointIsNotNamedByDefault_SoItCanBeMappedTwice()
	{
		// Endpoint names are globally unique: a hard-coded one would take down every route in the application
		// the moment someone maps the explanations under two api versions.
		await using WebApplication application = WebApplication.CreateBuilder().Build();

		application.MapProblemExplanations("/v1/problems/{**problemType}");
		application.MapProblemExplanations("/v2/problems/{**problemType}");

		List<Endpoint> endpoints = ((IEndpointRouteBuilder)application).DataSources
			.SelectMany(source => source.Endpoints).ToList();

		await Assert.That(endpoints.Count).IsEqualTo(2);
		await Assert.That(endpoints.All(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>() == null))
			.IsTrue();
	}
}
