namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Tests.Producers;

/// <summary>
/// Verifies that the storage answers for the type a caller received, without ever handing out the catalogue.
/// </summary>
public class ProblemStorageTests
{
	[Test]
	public async Task Storage_AnswersForTheTypeOfADeclaredProblem()
	{
		ProblemStorage storage = ProblemStorageTests.CreateStorage();

		ExplainedProblemDetails? details = storage.GetProblemDetails("/problems/test/public");

		await Assert.That(details).IsNotNull();
		await Assert.That(details!.Title).IsEqualTo("A public problem");
		await Assert.That(details.Explanation).IsEqualTo("Public properties are found as well.");
	}

	[Test]
	public async Task Storage_DoesNotAnswerForAnUnknownType()
	{
		ProblemStorage storage = ProblemStorageTests.CreateStorage();

		await Assert.That(storage.GetProblemDetails("/problems/test/nothing-like-this")).IsNull();
	}

	[Test]
	public async Task Storage_MergesEveryProducer()
	{
		ProblemStorage storage = ProblemStorageTests.CreateStorage();

		await Assert.That(storage.GetProblemDetails("/problems/test/other")).IsNotNull();
		await Assert.That(storage.GetProblemDetails("/problems/test/internal")).IsNotNull();
		await Assert.That(storage.AllProblems.Count).IsEqualTo(4);
	}

	[Test]
	public async Task Storage_HandsOutACopy()
	{
		ProblemStorage storage = ProblemStorageTests.CreateStorage();

		ExplainedProblemDetails first = storage.GetProblemDetails("/problems/test/public")!;
		first.Extensions["userId"] = 42;
		first.Title = "Rewritten";

		ExplainedProblemDetails second = storage.GetProblemDetails("/problems/test/public")!;

		// The storage is a singleton shared by every request. What one caller adds must not reach the next.
		await Assert.That(object.ReferenceEquals(first, second)).IsFalse();
		await Assert.That(second.Extensions.ContainsKey("userId")).IsFalse();
		await Assert.That(second.Title).IsEqualTo("A public problem");
	}

	[Test]
	public async Task Storage_HandsOutCopiesOfTheWholeCatalogueToo()
	{
		ProblemStorage storage = ProblemStorageTests.CreateStorage();

		foreach (ExplainedProblemDetails problem in storage.AllProblems)
		{
			problem.Extensions["leaked"] = true;
		}

		await Assert.That(storage.AllProblems.Any(problem => problem.Extensions.ContainsKey("leaked"))).IsFalse();
	}

	[Test]
	public async Task TwoProducersDeclaringTheSameType_AreReported()
	{
		InvalidOperationException? failure = null;
		try
		{
			_ = new ProblemStorage([new TestProblems(), new ClashingProducer()]);
		}
		catch (InvalidOperationException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("/problems/test/internal");
	}

	[Test]
	public async Task OneProducerDeclaringTheSameTypeTwice_IsReported()
	{
		InvalidOperationException? failure = null;
		try
		{
			_ = new DuplicateWithinProducer();
		}
		catch (InvalidOperationException exception)
		{
			failure = exception;
		}

		await Assert.That(failure).IsNotNull();
		await Assert.That(failure!.Message).Contains("/problems/duplicate");
		await Assert.That(failure.Message).Contains(nameof(DuplicateWithinProducer.First));
	}

	private static ProblemStorage CreateStorage()
	{
		return new ProblemStorage([new TestProblems(), new OtherTestProblems()]);
	}
}
