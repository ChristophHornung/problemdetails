namespace Chorn.AspNetCore.ProblemDetails;

using System.Reflection;

/// <summary>
/// Configures what
/// <see cref="ExplainedProblemDetailsServiceCollectionExtensions.AddExplainedProblemDetails"/> registers.
/// </summary>
/// <remarks>
/// The defaults are what an application wants: the application and the libraries it depends on that use this
/// package are scanned for producers, the exception handler and the framework problem details services are
/// registered, and every documented problem name is resolved once so a stale <c>nameof</c> fails the startup
/// rather than the api description.
/// </remarks>
public sealed class ExplainedProblemDetailsOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether every problem named by a
	/// <see cref="ProducesProblemsAttribute{TProducer}"/> is resolved during startup.
	/// </summary>
	/// <remarks>
	/// This is the one check the compiler cannot do: a <c>nameof</c> that resolves against a different producer
	/// than the documented one. Turning it off saves a single reflection pass over the scanned assemblies.
	/// </remarks>
	public bool ValidateDocumentedProblems { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether <see cref="ExplainedProblemExceptionHandler"/> is registered.
	/// </summary>
	/// <remarks>
	/// Without it an <see cref="ExplainedProblemException"/> is just an unhandled exception. Turn it off only to
	/// register the handler yourself, in a specific position of the handler chain.
	/// </remarks>
	public bool RegisterExceptionHandler { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether <c>AddProblemDetails</c> is called on the service collection.
	/// </summary>
	/// <remarks>
	/// That is the framework service which turns unhandled failures and bare status codes into problem
	/// responses. Turn it off if the application configures it itself.
	/// </remarks>
	public bool AddProblemDetailsServices { get; set; } = true;

	/// <summary>
	/// Gets the assemblies scanned for <see cref="IProblemProducer"/> implementations.
	/// </summary>
	internal HashSet<Assembly> Assemblies { get; } = [];

	/// <summary>
	/// Gets the producers registered explicitly rather than found by scanning.
	/// </summary>
	internal HashSet<Type> Producers { get; } = [];

	/// <summary>
	/// Scans exactly the given assemblies for problem producers, instead of working out the application.
	/// </summary>
	/// <param name="assemblies">The assemblies to scan.</param>
	/// <returns>The options, to chain calls on.</returns>
	public ExplainedProblemDetailsOptions ScanAssemblies(params Assembly[] assemblies)
	{
		foreach (Assembly assembly in assemblies)
		{
			this.Assemblies.Add(assembly);
		}

		return this;
	}

	/// <summary>
	/// Scans exactly the assembly the given type lives in, instead of working out the application.
	/// </summary>
	/// <typeparam name="TMarker">Any type of the assembly to scan.</typeparam>
	/// <returns>The options, to chain calls on.</returns>
	public ExplainedProblemDetailsOptions ScanAssemblyOf<TMarker>()
	{
		return this.ScanAssemblies(typeof(TMarker).Assembly);
	}

	/// <summary>
	/// Registers a single producer, for an application that would rather not be scanned at all.
	/// </summary>
	/// <typeparam name="TProducer">The producer to register.</typeparam>
	/// <returns>The options, to chain calls on.</returns>
	public ExplainedProblemDetailsOptions AddProducer<TProducer>()
		where TProducer : class, IProblemProducer
	{
		return this.AddProducer(typeof(TProducer));
	}

	/// <summary>
	/// Registers a single producer, for an application that would rather not be scanned at all.
	/// </summary>
	/// <param name="producerType">The producer to register.</param>
	/// <returns>The options, to chain calls on.</returns>
	/// <exception cref="ArgumentException">if the type is not a concrete <see cref="IProblemProducer"/>.</exception>
	public ExplainedProblemDetailsOptions AddProducer(Type producerType)
	{
		ArgumentNullException.ThrowIfNull(producerType);

		if (!ExplainedProblemDetailsOptions.IsProducer(producerType))
		{
			throw new ArgumentException(
				$"{producerType.Name} is not a concrete, closed class implementing {nameof(IProblemProducer)}.",
				nameof(producerType));
		}

		this.Producers.Add(producerType);
		return this;
	}

	/// <summary>
	/// Gets a value indicating whether the given type is a producer the container can create.
	/// </summary>
	/// <param name="candidate">The type to check.</param>
	/// <returns><c>true</c> if it is a producer.</returns>
	/// <remarks>
	/// An open generic producer is excluded: it satisfies every other check, and registering it as an
	/// implementation type fails the container at the first resolve with an error that names neither this
	/// package nor the mistake.
	/// </remarks>
	internal static bool IsProducer(Type candidate)
	{
		return typeof(IProblemProducer).IsAssignableFrom(candidate) &&
		       candidate is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false };
	}
}
