namespace Chorn.AspNetCore.ProblemDetails;

/// <summary>
/// Endpoint metadata that names the problems an endpoint can answer with.
/// </summary>
/// <remarks>
/// Implemented by <see cref="ProducesProblemsAttribute{TProducer}"/>. The open api generation looks for this
/// interface rather than the attribute itself, because the attribute's producer type argument is not known there.
/// </remarks>
public interface IProducesProblems
{
	/// <summary>
	/// Gets the problems the endpoint can answer with.
	/// </summary>
	/// <returns>The documented problems.</returns>
	/// <exception cref="InvalidOperationException">if a named problem does not exist on the producer.</exception>
	IEnumerable<DocumentedProblem> GetProblems();
}
