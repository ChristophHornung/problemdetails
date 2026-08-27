namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// Holds every problem the application can produce, keyed by problem type.
/// </summary>
/// <remarks>
/// This is the other half of a problem response. The response carries only <c>Type</c>, <c>Title</c>,
/// <c>Detail</c> and <c>Status</c>; the <see cref="ExplainedProblemDetails.Explanation"/> stays here, and the
/// <c>Type</c> is what a developer brings back to look it up - by hand, or through the endpoint
/// <see cref="ProblemExplanationEndpointRouteBuilderExtensions.MapProblemExplanations"/> maps.
/// <para>
/// Every problem handed out is a copy. The storage is a singleton shared by every request, and a caller that
/// adds an extension to the problem it just looked up must not be able to write that into the catalogue.
/// </para>
/// </remarks>
public class ProblemStorage : IProblemStorage
{
	private readonly Dictionary<string, ExplainedProblemDetails> problems;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProblemStorage"/> class.
	/// </summary>
	/// <param name="problemProducers">Every producer registered in the application.</param>
	/// <exception cref="InvalidOperationException">if two producers declare the same problem type.</exception>
	public ProblemStorage(IEnumerable<IProblemProducer> problemProducers)
	{
		ArgumentNullException.ThrowIfNull(problemProducers);

		this.problems = new Dictionary<string, ExplainedProblemDetails>(StringComparer.Ordinal);

		foreach (IProblemProducer producer in problemProducers)
		{
			foreach (ExplainedProblemDetails problem in producer.AllPossibleProblems)
			{
				string type = problem.Type ?? string.Empty;

				if (this.problems.TryGetValue(type, out ExplainedProblemDetails? existing))
				{
					throw new InvalidOperationException(
						$"The problem type '{type}' is declared more than once - as '{existing.Title}' and as " +
						$"'{problem.Title}'. A type identifies a problem, so it has to be unique across all " +
						"producers.");
				}

				this.problems[type] = problem;
			}
		}
	}

	/// <inheritdoc />
	public IReadOnlyCollection<ExplainedProblemDetails> AllProblems =>
		this.problems.Values.Select(problem => problem.Copy()).ToList();

	/// <inheritdoc />
	public ExplainedProblemDetails? GetProblemDetails(string type)
	{
		ArgumentNullException.ThrowIfNull(type);

		return this.problems.TryGetValue(type, out ExplainedProblemDetails? details) ? details.Copy() : null;
	}
}
