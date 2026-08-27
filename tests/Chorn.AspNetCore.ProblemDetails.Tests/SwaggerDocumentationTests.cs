namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json;

/// <summary>
/// Verifies that a problem an endpoint declares reaches the Swashbuckle document.
/// </summary>
public class SwaggerDocumentationTests
{
	[Test]
	public async Task DeclaredProblem_IsAnExampleOfTheResponseForItsStatusCode()
	{
		(int status, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _ = document;

		await Assert.That(status).IsEqualTo(200);

		// The assign endpoint answers a conflict that no ProducesResponseType declares - the problem itself is
		// what puts the response into the document.
		JsonElement content = SwaggerDocumentationTests.ProblemContent(document, "/users/{userId}/assign", "post",
			"409");

		await Assert.That(content.GetProperty("schema").GetProperty("$ref").GetString())
			.IsEqualTo("#/components/schemas/ProblemDetails");

		JsonElement example = content.GetProperty("examples").GetProperty("NotSignedUp");

		await Assert.That(example.GetProperty("summary").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
		await Assert.That(example.GetProperty("description").GetString()).Contains("sign-up flow");
		await Assert.That(example.GetProperty("value").GetProperty("type").GetString())
			.IsEqualTo("/problems/user/not-signed-up");
		await Assert.That(example.GetProperty("value").GetProperty("status").GetInt32()).IsEqualTo(409);
	}

	[Test]
	public async Task ProblemsOfTheSameStatus_AreExamplesOfOneResponse()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _1 = document;

		JsonElement examples = SwaggerDocumentationTests
			.ProblemContent(document, "/users/{userId}/assign", "post", "409").GetProperty("examples");

		await Assert.That(examples.EnumerateObject().Count()).IsEqualTo(2);
		await Assert.That(examples.TryGetProperty("AlreadyAssigned", out JsonElement _2)).IsTrue();
	}

	[Test]
	public async Task DocumentationWithoutNames_DocumentsEveryProblemOfTheProducer()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _1 = document;

		// The delete endpoint names no problem, so all three of the producer are documented - two conflicts and
		// one forbidden.
		await Assert
			.That(SwaggerDocumentationTests.ProblemContent(document, "/users/{userId}", "delete", "409")
				.GetProperty("examples").EnumerateObject().Count()).IsEqualTo(2);
		JsonElement lastAdministrator = SwaggerDocumentationTests
			.ProblemContent(document, "/users/{userId}", "delete", "403")
			.GetProperty("examples").GetProperty("LastAdministrator").GetProperty("value");

		await Assert.That(lastAdministrator.GetProperty("status").GetInt32()).IsEqualTo(403);

		// The example is the body a caller receives, so a declared member belongs in it.
		await Assert.That(lastAdministrator.GetProperty("policy").GetString()).IsEqualTo("last-administrator");
	}

	[Test]
	public async Task MinimalApiEndpoint_IsDocumentedAsWell()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _1 = document;

		JsonElement example = SwaggerDocumentationTests
			.ProblemContent(document, "/orders/{orderId}/ship", "post", "409")
			.GetProperty("examples").GetProperty("AlreadyShipped");

		await Assert.That(example.GetProperty("value").GetProperty("type").GetString())
			.IsEqualTo("/problems/order/already-shipped");
	}

	[Test]
	public async Task Responses_StayInAscendingStatusCodeOrder()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _1 = document;

		string statusCodes = string.Join(",", document.RootElement.GetProperty("paths")
			.GetProperty("/users/{userId}").GetProperty("delete").GetProperty("responses").EnumerateObject()
			.Select(response => response.Name));

		await Assert.That(statusCodes).IsEqualTo("204,403,404,409");
	}

	[Test]
	public async Task ProblemsOfDifferentProducersSharingAName_AreBothDocumented()
	{
		(int _, JsonDocument document) = await SampleApplication.GetJsonAsync("/swagger/v1/swagger.json");

		using JsonDocument _1 = document;

		// Both producers declare an Unknown, and both are 404s - so the example keys are qualified rather than
		// one of them silently overwriting the other.
		JsonElement examples = SwaggerDocumentationTests
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
