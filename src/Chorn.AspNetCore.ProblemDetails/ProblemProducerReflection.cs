namespace Chorn.AspNetCore.ProblemDetails;

using System.Reflection;

/// <summary>
/// Reads the problems a producer declares as static properties.
/// </summary>
/// <remarks>
/// A producer never lists its problems by hand: both the runtime lookup (<see cref="ProblemProducerBase"/>) and
/// the open api documentation (<see cref="ProducesProblemsAttribute{TProducer}"/>) find them here, so adding a
/// property to a producer is all it takes for a problem to exist everywhere.
/// </remarks>
internal static class ProblemProducerReflection
{
	private const BindingFlags DeclaredProblems =
		BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

	/// <summary>
	/// Gets every problem the given producer declares, including the ones it inherits.
	/// </summary>
	/// <param name="producerType">The producer type to read.</param>
	/// <returns>All problems, each with the property name it is declared under.</returns>
	/// <remarks>
	/// The hierarchy is walked by hand because reflection does not return inherited static members, and a
	/// producer hierarchy - a base class holding the problems a group of producers share - is a reasonable
	/// thing to write. A problem redeclared in a derived producer hides the inherited one, as it would in C#.
	/// </remarks>
	/// <exception cref="InvalidOperationException">if reading a declared problem fails.</exception>
	public static IEnumerable<DocumentedProblem> GetProblems(Type producerType)
	{
		Dictionary<string, DocumentedProblem> problems = new(StringComparer.Ordinal);

		for (Type? declaring = producerType; declaring != null && declaring != typeof(object);
			 declaring = declaring.BaseType)
		{
			foreach (PropertyInfo property in declaring.GetProperties(ProblemProducerReflection.DeclaredProblems))
			{
				if (!typeof(ExplainedProblemDetails).IsAssignableFrom(property.PropertyType) ||
					property.GetMethod == null || property.GetIndexParameters().Length > 0 ||
					problems.ContainsKey(property.Name))
				{
					continue;
				}

				problems[property.Name] = new DocumentedProblem(property.Name,
					ProblemProducerReflection.Read(producerType, property), producerType);
			}
		}

		return problems.Values;
	}

	/// <summary>
	/// Gets the single problem the given producer declares under <paramref name="name"/>.
	/// </summary>
	/// <param name="producerType">The producer type to read.</param>
	/// <param name="name">The name of the declaring property.</param>
	/// <returns>The problem declared under that name.</returns>
	/// <exception cref="InvalidOperationException">if the producer declares no problem of that name.</exception>
	public static DocumentedProblem GetProblem(Type producerType, string name)
	{
		List<DocumentedProblem> problems = ProblemProducerReflection.GetProblems(producerType).ToList();

		foreach (DocumentedProblem problem in problems)
		{
			if (string.Equals(problem.Name, name, StringComparison.Ordinal))
			{
				return problem;
			}
		}

		throw new InvalidOperationException(
			$"'{name}' is not a problem declared by {producerType.Name}. It declares: " +
			$"{string.Join(", ", problems.Select(p => p.Name))}.");
	}

	/// <summary>
	/// Reads a declared problem, naming the property when that fails.
	/// </summary>
	/// <param name="producerType">The producer being read, for the error message.</param>
	/// <param name="property">The property declaring the problem.</param>
	/// <returns>The problem.</returns>
	/// <exception cref="InvalidOperationException">if the property throws or returns <c>null</c>.</exception>
	private static ExplainedProblemDetails Read(Type producerType, PropertyInfo property)
	{
		object? value;
		try
		{
			value = property.GetMethod!.Invoke(null, null);
		}
		catch (TargetInvocationException exception) when (exception.InnerException != null)
		{
			throw new InvalidOperationException(
				$"Reading the problem '{producerType.Name}.{property.Name}' threw.", exception.InnerException);
		}

		if (value is not ExplainedProblemDetails details)
		{
			throw new InvalidOperationException(
				$"The problem '{producerType.Name}.{property.Name}' is null. A declared problem has to exist.");
		}

		return details;
	}
}
