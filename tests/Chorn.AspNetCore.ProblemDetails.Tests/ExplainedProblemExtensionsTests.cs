namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Verifies what a declared problem turns into on its way out.
/// </summary>
public class ExplainedProblemExtensionsTests
{
	[Test]
	public async Task AsException_CarriesTheProblem()
	{
		ExplainedProblemDetails problem = TestProblems.InternalProblem;

		ExplainedProblemException exception = problem.AsException();

		await Assert.That(exception.ProblemDetails.Type).IsEqualTo(problem.Type);
		await Assert.That(exception.ProblemDetails.Explanation).IsEqualTo(problem.Explanation);
		await Assert.That(exception.Message).IsEqualTo(problem.Detail);
	}

	[Test]
	public async Task AsException_AddsTheGivenExtensions()
	{
		ExplainedProblemException exception = TestProblems.InternalProblem
			.AsException(new Dictionary<string, object?> { ["userId"] = 42 });

		await Assert.That(exception.ProblemDetails.Extensions.ContainsKey("userId")).IsTrue();
		await Assert.That(exception.ProblemDetails.Extensions["userId"]).IsEqualTo(42);
	}

	[Test]
	public async Task AsException_LeavesTheDeclaredProblemAlone()
	{
		ExplainedProblemDetails declared = TestProblems.InternalProblem;

		ExplainedProblemException exception =
			declared.AsException(new Dictionary<string, object?> { ["userId"] = 42 });

		// A declared problem may be a shared instance - one handed out by the storage always is - so a
		// per-request extension must never be written into it.
		await Assert.That(object.ReferenceEquals(declared, exception.ProblemDetails)).IsFalse();
		await Assert.That(declared.Extensions.ContainsKey("userId")).IsFalse();
	}

	[Test]
	public async Task AsException_KeepsExtensionsThatAreAlreadyThere()
	{
		ExplainedProblemDetails problem = TestProblems.InternalProblem;
		problem.Extensions["traceId"] = "abc";

		ExplainedProblemException exception =
			problem.AsException(new Dictionary<string, object?> { ["userId"] = 42 });

		await Assert.That(exception.ProblemDetails.Extensions.ContainsKey("traceId")).IsTrue();
		await Assert.That(exception.ProblemDetails.Extensions.ContainsKey("userId")).IsTrue();
	}

	[Test]
	public async Task Copy_SharesNothingWithTheOriginal()
	{
		ExplainedProblemDetails original = TestProblems.InternalProblem;
		original.Extensions["first"] = 1;

		ExplainedProblemDetails copy = original.Copy();
		copy.Extensions["second"] = 2;
		copy.Title = "Rewritten";

		await Assert.That(copy.Extensions.ContainsKey("first")).IsTrue();
		await Assert.That(copy.Explanation).IsEqualTo(original.Explanation);
		await Assert.That(original.Extensions.ContainsKey("second")).IsFalse();
		await Assert.That(original.Title).IsEqualTo("An internal problem");
	}

	[Test]
	public async Task PlainProblemDetails_BecomeAnExplainedProblemWithoutAnExplanation()
	{
		MvcProblemDetails problem = new()
		{
			Status = StatusCodes.Status418ImATeapot,
			Type = "/problems/test/teapot",
			Title = "I am a teapot",
			Detail = "Short and stout.",
			Instance = "/brew"
		};
		problem.Extensions["pot"] = "small";

		ExplainedProblemException exception = problem.AsException();
		exception.ProblemDetails.Extensions["added"] = true;

		await Assert.That(exception.ProblemDetails.Status).IsEqualTo(StatusCodes.Status418ImATeapot);
		await Assert.That(exception.ProblemDetails.Type).IsEqualTo("/problems/test/teapot");
		await Assert.That(exception.ProblemDetails.Title).IsEqualTo("I am a teapot");
		await Assert.That(exception.ProblemDetails.Detail).IsEqualTo("Short and stout.");
		await Assert.That(exception.ProblemDetails.Instance).IsEqualTo("/brew");
		await Assert.That(exception.ProblemDetails.Explanation).IsNull();
		await Assert.That(exception.ProblemDetails.Extensions["pot"]).IsEqualTo("small");
		await Assert.That(problem.Extensions.ContainsKey("added")).IsFalse();
	}

	[Test]
	public async Task ResponseJson_IsWhatTheProblemDeclares()
	{
		JsonObject json = TestProblems.InternalProblem.ToResponseJson();

		await Assert.That(json["type"]!.GetValue<string>()).IsEqualTo("/problems/test/internal");
		await Assert.That(json["title"]!.GetValue<string>()).IsEqualTo("An internal problem");
		await Assert.That(json["status"]!.GetValue<int>()).IsEqualTo(409);
		await Assert.That(json["detail"]!.GetValue<string>()).IsEqualTo("Declared by an internal property.");
	}

	[Test]
	public async Task ResponseJson_LeavesTheExplanationOnTheServer()
	{
		JsonObject json = TestProblems.InternalProblem.ToResponseJson();

		await Assert.That(json.ContainsKey("explanation")).IsFalse();
		await Assert.That(json.Count).IsEqualTo(4);
	}

	[Test]
	public async Task ResponseJson_OmitsWhatTheProblemDoesNotSay()
	{
		JsonObject json = new ExplainedProblemDetails { Title = "Only a title" }.ToResponseJson();

		await Assert.That(json.Count).IsEqualTo(1);
		await Assert.That(json.ContainsKey("title")).IsTrue();
	}
}
