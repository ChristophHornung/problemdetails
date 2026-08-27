namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json;

/// <summary>
/// Verifies that a problem an endpoint declares reaches the built-in open api document.
/// </summary>
public class OpenApiDocumentationTests
{
	[Test]
	public async Task DeclaredProblem_IsAnExampleOfTheResponseForItsStatusCode()
	{
		(int status, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _ = document;

		await Assert.That(status).IsEqualTo(200);

		JsonElement content = OpenApiDocumentationTests.ProblemContent(document, "/users/{userId}/assign", "post",
			"409");

		JsonElement example = content.GetProperty("examples").GetProperty("NotSignedUp");

		await Assert.That(example.GetProperty("summary").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
		await Assert.That(example.GetProperty("description").GetString()).Contains("sign-up flow");
		await Assert.That(example.GetProperty("value").GetProperty("status").GetInt32()).IsEqualTo(409);
	}

	[Test]
	public async Task ProblemDetailsSchema_IsWrittenOutOnceAndReferenced()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _1 = document;

		await Assert
			.That(OpenApiDocumentationTests.ProblemContent(document, "/users/{userId}/assign", "post", "409")
				.GetProperty("schema").GetProperty("$ref").GetString())
			.IsEqualTo("#/components/schemas/ProblemDetails");

		await Assert.That(document.RootElement.GetProperty("components").GetProperty("schemas")
			.TryGetProperty("ProblemDetails", out JsonElement _2)).IsTrue();
	}

	[Test]
	public async Task ResponseTheEndpointDeclaresItself_KeepsItsDescription()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _1 = document;

		// The 403 is declared by a ProducesResponseType and documented by a <response> comment - the problem
		// adds its example to it rather than replacing the response.
		JsonElement forbidden = document.RootElement.GetProperty("paths").GetProperty("/users/{userId}")
			.GetProperty("delete").GetProperty("responses").GetProperty("403");

		await Assert.That(forbidden.GetProperty("description").GetString())
			.IsEqualTo("The user is the last administrator of their tenant.");
		await Assert.That(forbidden.GetProperty("content").GetProperty("application/problem+json")
			.GetProperty("examples").TryGetProperty("LastAdministrator", out JsonElement _2)).IsTrue();
	}

	[Test]
	public async Task StatusCodeOnlyAProblemProduces_GetsItsOwnResponse()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _1 = document;

		// Nothing declares the 409 - the problem is what puts the response into the document, described by the
		// reason phrase of its status code.
		JsonElement conflict = document.RootElement.GetProperty("paths").GetProperty("/users/{userId}/assign")
			.GetProperty("post").GetProperty("responses").GetProperty("409");

		await Assert.That(conflict.GetProperty("description").GetString()).IsEqualTo("Conflict");
	}

	[Test]
	public async Task MinimalApiEndpoint_IsDocumentedAsWell()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _1 = document;

		JsonElement example = OpenApiDocumentationTests
			.ProblemContent(document, "/orders/{orderId}/ship", "post", "409")
			.GetProperty("examples").GetProperty("AlreadyShipped");

		await Assert.That(example.GetProperty("value").GetProperty("type").GetString())
			.IsEqualTo("/problems/order/already-shipped");
	}

	[Test]
	public async Task ProblemsOfDifferentProducersSharingAName_AreBothDocumented()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/openapi/v1.json");

		using JsonDocument _1 = document;

		// Both producers declare an Unknown, and both are 404s - so the example keys are qualified rather than
		// one of them silently overwriting the other.
		JsonElement examples = OpenApiDocumentationTests
			.ProblemContent(document, "/users/{userId}/orders/{orderId}", "get", "404")
			.GetProperty("examples");

		await Assert.That(examples.EnumerateObject().Count()).IsEqualTo(2);
		await Assert.That(examples.GetProperty("UserProblems.Unknown").GetProperty("value")
			.GetProperty("type").GetString()).IsEqualTo("/problems/user/unknown");
		await Assert.That(examples.GetProperty("OrderProblems.Unknown").GetProperty("value")
			.GetProperty("type").GetString()).IsEqualTo("/problems/order/unknown");
	}

	private static JsonElement ProblemContent(JsonDocument document, string path, string method, string status)
	{
		return document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method)
			.GetProperty("responses").GetProperty(status)
			.GetProperty("content").GetProperty("application/problem+json");
	}
}
