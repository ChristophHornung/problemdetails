namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json;
using Chorn.AspNetCore.ProblemDetails.Sample.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Hosts the sample application once for every test that talks to it.
/// </summary>
/// <remarks>
/// The entry point is named by a type of the sample assembly rather than by its <c>Program</c>, because the test
/// assembly has an entry point of its own.
/// </remarks>
internal static class SampleApplication
{
	private static readonly WebApplicationFactory<UserController> Factory = new();

	/// <summary>
	/// Gets a client for the hosted sample application.
	/// </summary>
	public static HttpClient Client { get; } = SampleApplication.Factory.CreateClient();

	/// <summary>
	/// Gets the response for the given route, parsed as json.
	/// </summary>
	/// <param name="route">The route to request.</param>
	/// <returns>The status code and the parsed body.</returns>
	public static async Task<(int Status, JsonDocument Body)> GetJsonAsync(string route)
	{
		using HttpResponseMessage response = await SampleApplication.Client.GetAsync(route);
		return ((int)response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()));
	}
}
