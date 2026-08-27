namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// A problem together with the name it is declared under and the producer that declares it.
/// </summary>
/// <param name="Name">The name of the static property declaring the problem, e.g. <c>DuplicateDiscovery</c>.</param>
/// <param name="Details">The problem itself.</param>
/// <param name="Producer">The producer the problem is declared on.</param>
/// <remarks>
/// The name is what a developer writes into <see cref="ProducesProblemsAttribute{TProducer}"/> and what the
/// generated open api document uses as the example key - the problem's <c>Type</c> is the identifier on the
/// wire, but it makes for a poor label. The producer disambiguates two problems that share a name.
/// </remarks>
public readonly record struct DocumentedProblem(string Name, ExplainedProblemDetails Details, Type Producer);
