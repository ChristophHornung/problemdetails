namespace Chorn.AspNetCore.ProblemDetails;

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Works out which assemblies to look for producers in, the way mvc works out where to look for controllers.
/// </summary>
/// <remarks>
/// Mvc starts from <see cref="IHostEnvironment.ApplicationName"/> rather than from whoever called it, and adds
/// the libraries the application depends on that in turn depend on mvc. The same two steps let the call sit in
/// a shared startup library and the problems in a domain library. The dependency file is read directly rather
/// than through Microsoft.Extensions.DependencyModel, so this package keeps no package references, and the graph
/// decides which libraries qualify, so only those are loaded.
/// </remarks>
internal static class ApplicationAssemblies
{
	/// <summary>
	/// Assemblies that never reference this package and are not worth loading to find that out.
	/// </summary>
	private static readonly string[] FrameworkPrefixes = ["System.", "Microsoft.", "Windows.", "runtime."];

	/// <summary>
	/// Gets the assemblies to scan for producers.
	/// </summary>
	/// <param name="application">The application assembly, see <see cref="ResolveApplication"/>.</param>
	/// <returns>The application and every dependency of it that uses this package.</returns>
	public static IReadOnlyCollection<Assembly> Discover(Assembly application)
	{
		string applicationName = application.GetName().Name!;
		string marker = typeof(IProblemProducer).Assembly.GetName().Name!;

		List<Assembly> found = [application];

		foreach (string candidate in ApplicationAssemblies.Candidates(application, applicationName, marker))
		{
			if (string.Equals(candidate, applicationName, StringComparison.OrdinalIgnoreCase) ||
				ApplicationAssemblies.TryLoad(candidate) is not { } assembly || found.Contains(assembly))
			{
				continue;
			}

			// The graph says it depends on this package; the assembly itself is what proves it uses it.
			if (ApplicationAssemblies.References(assembly, marker))
			{
				found.Add(assembly);
			}
		}

		return found;
	}

	/// <summary>
	/// Gets the assembly the application is, which is what the host is named after.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="callingAssembly">The assembly that called the registration.</param>
	/// <returns>The application assembly.</returns>
	/// <remarks>
	/// The host registers its environment as an instance, so it can be read off the collection before there is a
	/// provider - which is how mvc reads it too. Without a host the entry assembly is the application, and
	/// without one of those the caller is the best guess left.
	/// </remarks>
	public static Assembly ResolveApplication(IServiceCollection services, Assembly callingAssembly)
	{
		string? applicationName = ApplicationAssemblies.ApplicationNameOf(services);

		return (string.IsNullOrEmpty(applicationName) ? null : ApplicationAssemblies.TryLoad(applicationName!)) ??
		       Assembly.GetEntryAssembly() ?? callingAssembly;
	}

	/// <summary>
	/// Gets the names of the assemblies that may hold producers.
	/// </summary>
	/// <param name="application">The application assembly.</param>
	/// <param name="applicationName">Its simple name.</param>
	/// <param name="marker">The library id of this package.</param>
	/// <returns>The candidate assembly names.</returns>
	private static IEnumerable<string> Candidates(Assembly application, string applicationName, string marker)
	{
		return ApplicationAssemblies.FromDependencyFile(applicationName, marker) ??
			   ApplicationAssemblies.FromAssemblyReferences(application);
	}

	/// <summary>
	/// Takes the libraries the application depends on that in turn depend on this package.
	/// </summary>
	/// <param name="applicationName">The application to start from.</param>
	/// <param name="marker">The library id of this package.</param>
	/// <returns>Their assembly names, or <c>null</c> when no dependency file describes the application.</returns>
	/// <remarks>
	/// A project reference shows up here whether or not the application uses a type from it, which walking
	/// assembly references cannot see. A single file or trimmed application has no dependency file of its own
	/// and falls back to that walk - as mvc does, with the same loss.
	/// </remarks>
	private static IEnumerable<string>? FromDependencyFile(string applicationName, string marker)
	{
		Dictionary<string, Library>? graph = ApplicationAssemblies.ReadDependencyGraph();

		if (graph == null || !graph.ContainsKey(applicationName))
		{
			return null;
		}

		Dictionary<string, bool> uses = new(StringComparer.OrdinalIgnoreCase);
		HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

		foreach (string library in ApplicationAssemblies.Closure(graph, applicationName))
		{
			if (!ApplicationAssemblies.Uses(graph, library, marker, uses))
			{
				continue;
			}

			// A library id is a package id, which is not always the assembly name - Humanizer.Core holds
			// Humanizer.dll. The runtime files are the assemblies; the id is only a last resort.
			List<string> assemblies = graph[library].Assemblies;
			candidates.UnionWith(assemblies.Count > 0 ? assemblies : [library]);
		}

		return candidates;
	}

	/// <summary>
	/// Gets every library the given one depends on, directly or not.
	/// </summary>
	private static HashSet<string> Closure(Dictionary<string, Library> graph, string start)
	{
		HashSet<string> reached = new(StringComparer.OrdinalIgnoreCase);
		Queue<string> frontier = new();
		frontier.Enqueue(start);

		while (frontier.Count > 0)
		{
			if (!graph.TryGetValue(frontier.Dequeue(), out Library? library))
			{
				continue;
			}

			foreach (string dependency in library.Dependencies)
			{
				if (reached.Add(dependency))
				{
					frontier.Enqueue(dependency);
				}
			}
		}

		return reached;
	}

	/// <summary>
	/// Gets a value indicating whether the given library depends on this package, directly or not.
	/// </summary>
	private static bool Uses(Dictionary<string, Library> graph, string library, string marker,
		Dictionary<string, bool> known)
	{
		if (string.Equals(library, marker, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (known.TryGetValue(library, out bool answered))
		{
			return answered;
		}

		// Answering false while the answer is being worked out is what stops a cycle recurring forever.
		known[library] = false;

		bool uses = graph.TryGetValue(library, out Library? entry) && entry.Dependencies.Any(dependency =>
			ApplicationAssemblies.Uses(graph, dependency, marker, known));

		known[library] = uses;
		return uses;
	}

	/// <summary>
	/// Reads every library, its dependencies and its assemblies out of the dependency files of this process.
	/// </summary>
	/// <returns>The dependency graph by library id, or <c>null</c> when there is none to read.</returns>
	private static Dictionary<string, Library>? ReadDependencyGraph()
	{
		Dictionary<string, Library> graph = new(StringComparer.OrdinalIgnoreCase);

		foreach (string file in ApplicationAssemblies.DependencyFiles())
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(file));

				if (document.RootElement.ValueKind == JsonValueKind.Object &&
					document.RootElement.TryGetProperty("targets", out JsonElement targets) &&
					targets.ValueKind == JsonValueKind.Object)
				{
					foreach (JsonProperty target in targets.EnumerateObject())
					{
						ApplicationAssemblies.ReadTarget(target.Value, graph);
					}
				}
			}
			catch (Exception exception) when (exception is IOException or JsonException
												  or UnauthorizedAccessException or InvalidOperationException)
			{
				// An unreadable dependency file has to be no worse than not having one at all.
			}
		}

		return graph.Count == 0 ? null : graph;
	}

	private static void ReadTarget(JsonElement target, Dictionary<string, Library> graph)
	{
		if (target.ValueKind != JsonValueKind.Object)
		{
			return;
		}

		foreach (JsonProperty entry in target.EnumerateObject())
		{
			// The keys are 'Id/Version', while a dependency names the version separately.
			string id = entry.Name.Split('/')[0];

			if (!graph.TryGetValue(id, out Library? library))
			{
				library = new Library();
				graph[id] = library;
			}

			if (entry.Value.ValueKind == JsonValueKind.Object)
			{
				ApplicationAssemblies.ReadLibrary(entry.Value, library);
			}
		}
	}

	private static void ReadLibrary(JsonElement entry, Library library)
	{
		if (entry.TryGetProperty("dependencies", out JsonElement dependencies) &&
			dependencies.ValueKind == JsonValueKind.Object)
		{
			library.Dependencies.AddRange(dependencies.EnumerateObject().Select(dependency => dependency.Name));
		}

		foreach (string section in new[] { "runtime", "runtimeTargets" })
		{
			if (!entry.TryGetProperty(section, out JsonElement files) ||
				files.ValueKind != JsonValueKind.Object)
			{
				continue;
			}

			foreach (JsonProperty file in files.EnumerateObject())
			{
				// Skips the _._ placeholder of a library that carries nothing, and the native libraries a
				// runtime target lists next to the managed ones.
				bool managed = !file.Value.TryGetProperty("assetType", out JsonElement assetType) ||
				               assetType.ValueEquals("runtime");

				if (managed && file.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					library.Assemblies.Add(Path.GetFileNameWithoutExtension(file.Name));
				}
			}
		}
	}

	/// <summary>
	/// Gets the dependency files of this process, as the host itself resolved them.
	/// </summary>
	/// <returns>The files that exist.</returns>
	private static IEnumerable<string> DependencyFiles()
	{
		if (AppContext.GetData("APP_CONTEXT_DEPS_FILES") is string configured)
		{
			return configured.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(File.Exists);
		}

		string? entry = Assembly.GetEntryAssembly()?.GetName().Name;

		return entry == null
			? []
			: new[] { Path.Combine(AppContext.BaseDirectory, entry + ".deps.json") }.Where(File.Exists);
	}

	/// <summary>
	/// Walks the assembly references, for when no dependency file describes the application.
	/// </summary>
	/// <param name="application">The application assembly.</param>
	/// <returns>Every assembly name reachable from it.</returns>
	internal static IEnumerable<string> FromAssemblyReferences(Assembly application)
	{
		HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
		Queue<Assembly> frontier = new();
		frontier.Enqueue(application);

		while (frontier.Count > 0)
		{
			foreach (AssemblyName reference in frontier.Dequeue().GetReferencedAssemblies())
			{
				if (reference.Name == null || ApplicationAssemblies.IsFramework(reference.Name) ||
					!visited.Add(reference.Name))
				{
					continue;
				}

				if (ApplicationAssemblies.TryLoad(reference.FullName) is { } loaded)
				{
					frontier.Enqueue(loaded);
				}
			}
		}

		return visited;
	}

	private static string? ApplicationNameOf(IServiceCollection services)
	{
		for (int index = services.Count - 1; index >= 0; index--)
		{
			// IWebHostEnvironment derives from IHostEnvironment, so this finds either of them.
			if (services[index].ImplementationInstance is IHostEnvironment environment)
			{
				return environment.ApplicationName;
			}
		}

		return null;
	}

	private static bool IsFramework(string name)
	{
		return name is "netstandard" or "mscorlib" or "System" or "WindowsBase" ||
			   ApplicationAssemblies.FrameworkPrefixes.Any(prefix =>
				   name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
	}

	private static bool References(Assembly assembly, string marker)
	{
		return assembly.GetReferencedAssemblies().Any(reference =>
			string.Equals(reference.Name, marker, StringComparison.OrdinalIgnoreCase));
	}

	private static Assembly? TryLoad(string name)
	{
		try
		{
			// Parsed inside the try: a host may be given any application name, not only a valid assembly one.
			return Assembly.Load(new AssemblyName(name));
		}
		catch (Exception exception) when (exception is FileNotFoundException or FileLoadException
											  or BadImageFormatException)
		{
			// A reference that is not there at runtime cannot hold producers either.
			return null;
		}
	}

	/// <summary>
	/// One entry of the dependency file.
	/// </summary>
	private sealed class Library
	{
		/// <summary>
		/// Gets the library ids this one depends on.
		/// </summary>
		public List<string> Dependencies { get; } = [];

		/// <summary>
		/// Gets the assemblies this library carries, which need not be named after it.
		/// </summary>
		public List<string> Assemblies { get; } = [];
	}
}
