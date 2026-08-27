namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json;
using static Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users.UserDirectory;

/// <summary>
/// Verifies what a caller receives from the sample, on the paths that succeed and the ones that answer with a
/// declared problem.
/// </summary>
/// <remarks>
/// The host is shared and the tests run in parallel, so a test that changes a seeded user or order is the only
/// one to touch it: Cleo is assigned, Eve is removed, order 2 is shipped.
/// </remarks>
public class ProblemResponseTests
{
	[Test]
	public async Task ThrownProblem_BecomesTheProblemResponse()
	{
		using HttpResponseMessage response = await ProblemResponseTests.Assign(Known.Dan);

		await Assert.That((int)response.StatusCode).IsEqualTo(409);
		await Assert.That(response.Content.Headers.ContentType!.MediaType)
			.IsEqualTo("application/problem+json");

		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
		await Assert.That(body.RootElement.GetProperty("title").GetString())
			.IsEqualTo("User not yet signed up");
		await Assert.That(body.RootElement.GetProperty("status").GetInt32()).IsEqualTo(409);
		await Assert.That(body.RootElement.GetProperty("detail").GetString())
			.IsEqualTo("The user cannot be assigned because they have not signed up yet.");
	}

	[Test]
	public async Task ThrownProblem_CarriesTheExtensionsItWasGiven()
	{
		using HttpResponseMessage response = await ProblemResponseTests.Assign(Known.Dan);
		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.GetProperty("userId").GetString()).IsEqualTo(Known.Dan.ToString());
	}

	[Test]
	public async Task ProblemResponse_LeavesTheExplanationOnTheServer()
	{
		using HttpResponseMessage response = await ProblemResponseTests.Assign(Known.Dan);
		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.TryGetProperty("explanation", out JsonElement _)).IsFalse();
	}

	[Test]
	public async Task ReturnedProblem_BecomesTheSameResponse()
	{
		using HttpResponseMessage response = await SampleApplication.Client.DeleteAsync($"/users/{Known.Ada}");

		await Assert.That((int)response.StatusCode).IsEqualTo(403);

		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/user/last-administrator");
		await Assert.That(body.RootElement.GetProperty("instance").GetString()).IsEqualTo($"/users/{Known.Ada}");
	}

	[Test]
	public async Task ProblemDeclaredOnTheController_IsAnsweredByEveryEndpoint()
	{
		using HttpResponseMessage response = await ProblemResponseTests.Assign(Guid.NewGuid());

		await Assert.That((int)response.StatusCode).IsEqualTo(404);
		await Assert.That(await ProblemResponseTests.TypeOf(response)).IsEqualTo("/problems/user/unknown");
	}

	[Test]
	public async Task MinimalApiEndpoint_AnswersWithAProblemJustLikeAController()
	{
		using HttpResponseMessage response = await SampleApplication.Client.PostAsync("/orders/1/ship", null);

		await Assert.That((int)response.StatusCode).IsEqualTo(409);

		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

		await Assert.That(body.RootElement.GetProperty("type").GetString())
			.IsEqualTo("/problems/order/already-shipped");
		await Assert.That(body.RootElement.GetProperty("orderId").GetInt32()).IsEqualTo(1);
	}

	[Test]
	public async Task SomebodyElsesOrder_IsAsUnknownAsNoOrder()
	{
		using HttpResponseMessage response =
			await SampleApplication.Client.GetAsync($"/users/{Known.Ada}/orders/3");

		await Assert.That((int)response.StatusCode).IsEqualTo(404);
		await Assert.That(await ProblemResponseTests.TypeOf(response)).IsEqualTo("/problems/order/unknown");
	}

	[Test]
	public async Task AssigningASignedUpUser_Succeeds()
	{
		using HttpResponseMessage response = await ProblemResponseTests.Assign(Known.Cleo);

		await Assert.That((int)response.StatusCode).IsEqualTo(204);
	}

	[Test]
	public async Task RemovingAnOrdinaryMember_Succeeds()
	{
		using HttpResponseMessage response = await SampleApplication.Client.DeleteAsync($"/users/{Known.Eve}");

		await Assert.That((int)response.StatusCode).IsEqualTo(204);
	}

	[Test]
	public async Task ShippingAnOpenOrder_Succeeds()
	{
		using HttpResponseMessage response = await SampleApplication.Client.PostAsync("/orders/2/ship", null);

		await Assert.That((int)response.StatusCode).IsEqualTo(204);
	}

	[Test]
	public async Task OwnOrder_IsReturned()
	{
		(int status, JsonDocument body) = await SampleApplication.GetJsonAsync($"/users/{Known.Ada}/orders/1");

		using JsonDocument _ = body;

		await Assert.That(status).IsEqualTo(200);
		await Assert.That(body.RootElement.GetProperty("description").GetString()).IsEqualTo("A difference engine");
	}

	private static Task<HttpResponseMessage> Assign(Guid userId)
	{
		return SampleApplication.Client.PostAsync($"/users/{userId}/assign", content: null);
	}

	private static async Task<string?> TypeOf(HttpResponseMessage response)
	{
		using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		return body.RootElement.GetProperty("type").GetString();
	}
}
