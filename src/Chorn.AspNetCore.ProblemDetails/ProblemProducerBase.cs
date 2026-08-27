namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// The base class for a problem producer. All problem details are loaded via reflection on
/// <see cref="ExplainedProblemDetailsServiceCollectionExtensions.AddExplainedProblemDetails"/>, so no additional
/// wiring is required once the producer exists.
/// </summary>
/// <remarks>
/// Declare one static property per problem, as an expression body so every reader gets its own instance. The
/// property may be <c>internal</c>, and so may the producer itself:
/// <example>
/// <code>
/// internal sealed class UserProblems : ProblemProducerBase
/// {
///     internal static ExplainedProblemDetails NotSignedUp => new()
///     {
///         Title = "User not yet signed up",
///         Detail = "The user cannot be assigned because they have not signed up yet.",
///         Status = StatusCodes.Status409Conflict,
///         Type = "/problems/user/not-signed-up",
///         Explanation = "Have the user sign up first, then assign them to a tenant."
///     };
/// }
/// </code>
/// </example>
/// A producer may derive from another producer to share problems, and needs a public parameterless constructor
/// so the container can create it.
/// </remarks>
public abstract class ProblemProducerBase : IProblemProducer
{
	private readonly List<ExplainedProblemDetails> problems;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProblemProducerBase"/> class, reading every problem the
	/// concrete producer declares.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// if the producer declares two problems of the same type, or if reading one of them fails.
	/// </exception>
	protected ProblemProducerBase()
	{
		List<DocumentedProblem> problems = ProblemProducerReflection.GetProblems(this.GetType()).ToList();

		IGrouping<string, DocumentedProblem>? duplicate = problems
			.GroupBy(problem => problem.Details.Type ?? string.Empty, StringComparer.Ordinal)
			.FirstOrDefault(group => group.Count() > 1);

		if (duplicate != null)
		{
			throw new InvalidOperationException(
				$"{this.GetType().Name} declares {string.Join(" and ", duplicate.Select(problem => problem.Name))} " +
				$"with the same type '{duplicate.Key}'. A type identifies a problem, so it has to be unique.");
		}

		this.problems = problems.Select(problem => problem.Details).ToList();
	}

	/// <inheritdoc />
	/// <remarks>
	/// A copy each time. A producer is a singleton and its problems are what the storage is built from, so a
	/// caller that adds a member to one it read here must not be able to write that into the catalogue.
	/// </remarks>
	public IEnumerable<ExplainedProblemDetails> AllPossibleProblems =>
		this.problems.Select(problem => problem.Copy());
}
