namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// Declares which problems an endpoint can answer with, so they show up in the api description as
/// <c>application/problem+json</c> responses - one response per status code, one example per problem.
/// </summary>
/// <typeparam name="TProducer">The producer declaring the problems, e.g. <c>UserProblems</c>.</typeparam>
/// <remarks>
/// <para>
/// Name the problems with <c>nameof</c> - statically importing the producer keeps that short. Leave the list
/// empty to document every problem the producer declares. The names are resolved at startup as well, which
/// catches a name that belongs to a different producer than the one documented here.
/// </para>
/// <para>
/// The attribute may sit on the controller - where it applies to every endpoint in it - or on a single
/// endpoint, and may be repeated to document problems from more than one producer.
/// </para>
/// <example>
/// <code>
/// using static MyApi.Problems.UserProblems;
///
/// [ProducesProblems&lt;UserProblems&gt;(nameof(NotSignedUp))]
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ProducesProblemsAttribute<TProducer> : Attribute, IProducesProblems
	where TProducer : class, IProblemProducer, new()
{
	private readonly string[] problemNames;

	/// <summary>
	/// Initializes a new instance of the <see cref="ProducesProblemsAttribute{TProducer}"/> class.
	/// </summary>
	/// <param name="problemNames">
	/// The names of the problems, i.e. <c>nameof</c> of the properties they are declared under on
	/// <typeparamref name="TProducer"/>. Pass none to document every problem the producer declares.
	/// </param>
	public ProducesProblemsAttribute(params string[] problemNames)
	{
		this.problemNames = problemNames;
	}

	/// <inheritdoc />
	public IEnumerable<DocumentedProblem> GetProblems()
	{
		return this.problemNames.Length == 0
			? ProblemProducerReflection.GetProblems(typeof(TProducer))
			: this.problemNames.Select(name => ProblemProducerReflection.GetProblem(typeof(TProducer), name));
	}
}
