namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Compiles a snippet and runs one analyzer over it.
/// </summary>
/// <remarks>
/// The references are the assemblies this test host already runs on - the shared framework and everything next
/// to the test assembly, which is where this package ends up. That keeps the snippets compiling against the
/// same api surface a consumer would.
/// </remarks>
internal static class AnalyzerHarness
{
	private static readonly Lazy<List<MetadataReference>> KnownReferences = new(AnalyzerHarness.LoadReferences);

	/// <summary>
	/// Runs the analyzer over the given source.
	/// </summary>
	/// <param name="analyzer">The analyzer to run.</param>
	/// <param name="source">The source to analyze.</param>
	/// <returns>The diagnostics it reported, in source order.</returns>
	/// <exception cref="InvalidOperationException">if the snippet does not compile.</exception>
	public static async Task<List<Diagnostic>> RunAsync(DiagnosticAnalyzer analyzer, string source)
	{
		CSharpCompilation compilation = CSharpCompilation.Create("AnalyzerSnippet",
			[CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
			AnalyzerHarness.KnownReferences.Value,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
				nullableContextOptions: NullableContextOptions.Enable));

		List<Diagnostic> failures = compilation.GetDiagnostics()
			.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();

		if (failures.Count > 0)
		{
			throw new InvalidOperationException(
				"The snippet does not compile: " + string.Join("; ", failures.Select(f => f.GetMessage())));
		}

		ImmutableArray<Diagnostic> reported = await compilation
			.WithAnalyzers(ImmutableArray.Create(analyzer))
			.GetAnalyzerDiagnosticsAsync();

		return reported.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ToList();
	}

	private static List<MetadataReference> LoadReferences()
	{
		Dictionary<string, MetadataReference> byName = new(StringComparer.OrdinalIgnoreCase);

		foreach (string directory in new[]
				 {
					 Path.GetDirectoryName(typeof(object).Assembly.Location)!,
					 Path.GetDirectoryName(typeof(Microsoft.AspNetCore.Http.Results).Assembly.Location)!,
					 AppContext.BaseDirectory
				 })
		{
			foreach (string file in Directory.GetFiles(directory, "*.dll"))
			{
				string name = Path.GetFileNameWithoutExtension(file);
				if (byName.ContainsKey(name))
				{
					continue;
				}

				try
				{
					// Both directories hold native libraries as well, and CreateFromFile only notices when the
					// compilation reads them - by which point it is an unhelpful compile error.
					_ = System.Reflection.AssemblyName.GetAssemblyName(file);
					byName[name] = MetadataReference.CreateFromFile(file);
				}
				catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
				{
					// Not a managed assembly, so nothing to reference.
				}
			}
		}

		return byName.Values.ToList();
	}
}
