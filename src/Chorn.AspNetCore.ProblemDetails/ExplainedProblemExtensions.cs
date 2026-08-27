namespace Chorn.AspNetCore.ProblemDetails;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// The extensions that turn a declared problem into something to throw, to return or to document.
/// </summary>
public static class ExplainedProblemExtensions
{
	/// <summary>
	/// Copies the problem, so the copy can be given per-request members without touching the original.
	/// </summary>
	/// <param name="problemDetails">The problem to copy.</param>
	/// <returns>A copy, sharing nothing with the original.</returns>
	/// <remarks>
	/// A declaration may be a shared instance, and one held by a producer or the storage always is, so everything
	/// here that adds to a problem copies it first.
	/// </remarks>
	public static ExplainedProblemDetails Copy(this ExplainedProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);

		ExplainedProblemDetails copy = new()
		{
			Type = problemDetails.Type,
			Title = problemDetails.Title,
			Status = problemDetails.Status,
			Detail = problemDetails.Detail,
			Instance = problemDetails.Instance,
			Explanation = problemDetails.Explanation
		};

		foreach ((string key, object? value) in problemDetails.Extensions)
		{
			copy.Extensions[key] = value;
		}

		return copy;
	}

	/// <summary>
	/// Wraps a copy of the problem in an exception to throw from anywhere in the call stack.
	/// </summary>
	/// <param name="problemDetails">The problem to answer with.</param>
	/// <param name="extensions">Additional members to add to the problem response, e.g. the offending id.</param>
	/// <returns>The exception to throw.</returns>
	/// <remarks>
	/// <see cref="ExplainedProblemExceptionHandler"/> turns the exception into the response, so this is how a
	/// service that knows nothing about HTTP answers with a documented problem. The extensions go onto the copy
	/// the exception carries, never onto the declaration.
	/// </remarks>
	public static ExplainedProblemException AsException(this ExplainedProblemDetails problemDetails,
		IDictionary<string, object?>? extensions = null)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);

		ExplainedProblemDetails carried = problemDetails.Copy();

		foreach ((string key, object? value) in extensions ?? new Dictionary<string, object?>())
		{
			carried.Extensions[key] = value;
		}

		return new ExplainedProblemException(carried);
	}

	/// <summary>
	/// Wraps a plain problem details in an exception to throw from anywhere in the call stack.
	/// </summary>
	/// <param name="problemDetails">The problem to answer with.</param>
	/// <returns>The exception to throw.</returns>
	/// <remarks>
	/// For a problem built on the spot rather than declared. It gains no
	/// <see cref="ExplainedProblemDetails.Explanation"/>.
	/// </remarks>
	public static ExplainedProblemException AsException(this MvcProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);

		ExplainedProblemDetails carried = new()
		{
			Status = problemDetails.Status,
			Type = problemDetails.Type,
			Title = problemDetails.Title,
			Detail = problemDetails.Detail,
			Instance = problemDetails.Instance
		};

		foreach ((string key, object? value) in problemDetails.Extensions)
		{
			carried.Extensions[key] = value;
		}

		return new ExplainedProblemException(carried);
	}

	/// <summary>
	/// Answers the request with the given problem.
	/// </summary>
	/// <param name="controller">The controller answering the request.</param>
	/// <param name="problemDetails">The problem to answer with.</param>
	/// <param name="instance">The instance the problem occurred on.</param>
	/// <param name="extensions">Additional members to add to the problem response.</param>
	/// <returns>The problem response.</returns>
	/// <remarks>
	/// An instance method beats an extension method, so a controller base declaring a <c>Problem</c> that takes a
	/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> would silently take these calls over and serialize
	/// the declared problem whole. That is what the CHPD001 analyzer rule reports.
	/// </remarks>
	public static ObjectResult Problem(this ControllerBase controller, MvcProblemDetails problemDetails,
		string? instance = null, IDictionary<string, object?>? extensions = null)
	{
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(problemDetails);

		ObjectResult problemResponse = controller.Problem(problemDetails.Detail, instance, problemDetails.Status,
			problemDetails.Title, problemDetails.Type);

		// ControllerBase.Problem builds a fresh problem out of those five members; the declared extensions have
		// to be carried over by hand, as the thrown path does.
		IDictionary<string, object?> members = ((MvcProblemDetails)problemResponse.Value!).Extensions;

		foreach ((string key, object? value) in problemDetails.Extensions.Concat(
					 extensions ?? Enumerable.Empty<KeyValuePair<string, object?>>()))
		{
			members[key] = value;
		}

		return problemResponse;
	}

	/// <summary>
	/// Builds the json for the members this problem declares.
	/// </summary>
	/// <param name="problemDetails">The problem to render.</param>
	/// <returns>The declared members, without the server-side explanation.</returns>
	/// <remarks>
	/// The response example in the open api integrations. It is what the problem itself says, not the complete
	/// body: the framework adds a <c>traceId</c> at response time, and per-request extensions are not known here.
	/// </remarks>
	public static JsonObject ToResponseJson(this ExplainedProblemDetails problemDetails)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);

		JsonObject value = new();

		if (problemDetails.Type != null)
		{
			value["type"] = problemDetails.Type;
		}

		if (problemDetails.Title != null)
		{
			value["title"] = problemDetails.Title;
		}

		if (problemDetails.Status != null)
		{
			value["status"] = problemDetails.Status.Value;
		}

		if (problemDetails.Detail != null)
		{
			value["detail"] = problemDetails.Detail;
		}

		if (problemDetails.Instance != null)
		{
			value["instance"] = problemDetails.Instance;
		}

		ExplainedProblemExtensions.AddExtensions(problemDetails, value);

		return value;
	}

	/// <summary>
	/// Adds the members the problem declares beyond the ones rfc 9457 names.
	/// </summary>
	/// <param name="problemDetails">The problem to read.</param>
	/// <param name="value">The json to add them to.</param>
	private static void AddExtensions(ExplainedProblemDetails problemDetails, JsonObject value)
	{
		foreach ((string key, object? extension) in problemDetails.Extensions)
		{
			if (value.ContainsKey(key))
			{
				continue;
			}

			try
			{
				value[key] = extension == null ? null : JsonSerializer.SerializeToNode(extension);
			}
			catch (Exception exception) when (exception is NotSupportedException or JsonException)
			{
				// A member that cannot be written is left out rather than failing the api description.
			}
		}
	}
}
