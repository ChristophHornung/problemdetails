namespace Chorn.AspNetCore.ProblemDetails;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers the explained problem details services.
/// </summary>
public static class ExplainedProblemDetailsServiceCollectionExtensions
{
	private const BindingFlags AnyMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
										   BindingFlags.Static | BindingFlags.DeclaredOnly;

	/// <summary>
	/// Adds all services required for the explained problem details.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configures what is registered, see <see cref="ExplainedProblemDetailsOptions"/>.</param>
	/// <returns>The service collection with all parts registered.</returns>
	/// <exception cref="InvalidOperationException">
	/// if the application holds no producer at all, if a declared problem cannot be read, if a problem type is
	/// declared twice, or if a <see cref="ProducesProblemsAttribute{TProducer}"/> names a problem its producer
	/// does not declare.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Every <see cref="IProblemProducer"/> of the application is registered, so a new producer needs no further
	/// wiring. Pair it with <c>app.UseExceptionHandler()</c> so a thrown <see cref="ExplainedProblemException"/>
	/// reaches <see cref="ExplainedProblemExceptionHandler"/>.
	/// </para>
	/// <para>
	/// The application is the assembly the host is named after plus every dependency of it that uses this
	/// package - the rule mvc finds controllers by - so it does not matter whether the call sits in the web
	/// project or in a shared startup library, and a producer in a domain library counts.
	/// <see cref="ExplainedProblemDetailsOptions.ScanAssemblies"/> and
	/// <see cref="ExplainedProblemDetailsOptions.AddProducer{TProducer}"/> replace that with exactly what is
	/// named.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static IServiceCollection AddExplainedProblemDetails(this IServiceCollection services,
		Action<ExplainedProblemDetailsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		ExplainedProblemDetailsOptions options = new();
		configure?.Invoke(options);

		Assembly application = ApplicationAssemblies.ResolveApplication(services, Assembly.GetCallingAssembly());
		bool scanningImplicitly = options.Assemblies.Count == 0 && options.Producers.Count == 0;

		if (scanningImplicitly)
		{
			options.ScanAssemblies([.. ApplicationAssemblies.Discover(application)]);
		}

		HashSet<Type> producers = [.. options.Producers];
		foreach (Type candidate in options.Assemblies.SelectMany(
					 ExplainedProblemDetailsServiceCollectionExtensions.GetLoadableTypes))
		{
			if (ExplainedProblemDetailsOptions.IsProducer(candidate))
			{
				producers.Add(candidate);
			}
		}

		ExplainedProblemDetailsServiceCollectionExtensions.RequireProducers(producers, scanningImplicitly,
			options.Assemblies);

		foreach (Type producer in producers)
		{
			services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IProblemProducer), producer));
		}

		services.TryAddSingleton<ProblemStorage>();
		services.TryAddSingleton<IProblemStorage>(provider => provider.GetRequiredService<ProblemStorage>());

		if (options.RegisterExceptionHandler)
		{
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IExceptionHandler, ExplainedProblemExceptionHandler>());
		}

		if (options.AddProblemDetailsServices)
		{
			services.AddProblemDetails();
		}

		if (options.ValidateDocumentedProblems)
		{
			ExplainedProblemDetailsServiceCollectionExtensions.ValidateProblems(producers);

			// Whatever was scanned, wherever the producers came from, and the application - which holds the
			// endpoints, and which naming producers or assemblies explicitly would otherwise leave out.
			ExplainedProblemDetailsServiceCollectionExtensions.ValidateDocumentation(options.Assemblies
				.Concat(producers.Select(producer => producer.Assembly))
				.Append(application)
				.Distinct());
		}

		return services;
	}

	/// <summary>
	/// Fails when the assemblies worked out by default hold no producer, which almost always means the producers
	/// live somewhere the application does not reference.
	/// </summary>
	/// <param name="producers">The producers that were found.</param>
	/// <param name="scanningImplicitly">Whether the scanned assemblies were worked out rather than named.</param>
	/// <param name="scanned">The assemblies that were scanned.</param>
	/// <exception cref="InvalidOperationException">if nothing was found in assemblies nobody named.</exception>
	private static void RequireProducers(HashSet<Type> producers, bool scanningImplicitly,
		IEnumerable<Assembly> scanned)
	{
		if (producers.Count > 0 || !scanningImplicitly)
		{
			return;
		}

		throw new InvalidOperationException(
			$"No {nameof(IProblemProducer)} was found in " +
			$"{string.Join(", ", scanned.Select(assembly => assembly.GetName().Name))} - the application and " +
			"the assemblies it references that use this package. If the producers live elsewhere, name their " +
			"assembly with options.ScanAssemblyOf<T>(). A single-file or trimmed application has to.");
	}

	/// <summary>
	/// Reads every problem of every producer once, so a declared problem that throws, is null, or duplicates a
	/// type fails the startup rather than the first request that needs the storage.
	/// </summary>
	/// <param name="producers">The registered producers.</param>
	/// <exception cref="InvalidOperationException">if a problem cannot be read or a type is declared twice.</exception>
	/// <remarks>
	/// Only producers with a parameterless constructor are built here; the rest are left to the container.
	/// </remarks>
	private static void ValidateProblems(IEnumerable<Type> producers)
	{
		List<IProblemProducer> instances = [];

		foreach (Type producer in producers.Where(
					 producer => producer.GetConstructor(Type.EmptyTypes) != null))
		{
			try
			{
				instances.Add((IProblemProducer)Activator.CreateInstance(producer)!);
			}
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				// Activator wraps whatever the constructor threw, which would hide the message naming the
				// producer and the property that could not be read.
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			}
		}

		// Building the storage is what finds a type declared by two different producers.
		_ = new ProblemStorage(instances);
	}

	/// <summary>
	/// Resolves every problem documented via <see cref="ProducesProblemsAttribute{TProducer}"/> once, so a
	/// problem name that no longer exists fails the startup rather than the api description.
	/// </summary>
	/// <param name="assemblies">The assemblies to look through.</param>
	/// <exception cref="InvalidOperationException">if a documented problem does not exist on its producer.</exception>
	/// <remarks>
	/// Sees attributes on types and their methods only. Endpoint metadata on a minimal api is not reachable from
	/// here; the CHPD002 analyzer rule covers it at compile time.
	/// </remarks>
	private static void ValidateDocumentation(IEnumerable<Assembly> assemblies)
	{
		foreach (Type type in assemblies.SelectMany(
					 ExplainedProblemDetailsServiceCollectionExtensions.GetLoadableTypes))
		{
			foreach (IProducesProblems documented in
					 ExplainedProblemDetailsServiceCollectionExtensions.GetDocumentation(type))
			{
				// Enumerating is what resolves the names - and what throws for the ones that do not resolve.
				_ = documented.GetProblems().ToList();
			}
		}
	}

	/// <summary>
	/// Gets the problem documentation on a type and its methods, skipping a type that cannot be inspected.
	/// </summary>
	/// <param name="type">The type to read.</param>
	/// <returns>Every documentation found on it.</returns>
	private static IEnumerable<IProducesProblems> GetDocumentation(Type type)
	{
		try
		{
			return type.GetCustomAttributes(inherit: true)
				.Concat(type.GetMethods(ExplainedProblemDetailsServiceCollectionExtensions.AnyMethod)
					.SelectMany(method => method.GetCustomAttributes(inherit: true)))
				.OfType<IProducesProblems>()
				.ToList();
		}
		catch (Exception exception) when (exception is TypeLoadException or FileNotFoundException
											  or FileLoadException or BadImageFormatException)
		{
			// A type whose dependencies are missing cannot document anything that could be resolved here.
			return [];
		}
	}

	/// <summary>
	/// Gets the types of an assembly, skipping the ones that cannot be loaded.
	/// </summary>
	/// <param name="assembly">The assembly to read.</param>
	/// <returns>Every type that could be loaded.</returns>
	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException exception)
		{
			return exception.Types.OfType<Type>();
		}
	}
}
