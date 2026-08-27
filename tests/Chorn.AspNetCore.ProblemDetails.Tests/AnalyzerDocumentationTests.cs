namespace Chorn.AspNetCore.ProblemDetails.Tests;

using Chorn.AspNetCore.ProblemDetails.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Verifies that every rule links to a page, and that the page is there - an ide shows the rule id as a link to
/// it, so a missing one is a dead link in every consumer's error list.
/// </summary>
public class AnalyzerDocumentationTests
{
	private static readonly DiagnosticAnalyzer[] Analyzers =
	[
		new ExplanationLeakAnalyzer(), new DocumentedProblemNameAnalyzer(), new ProblemDeclarationAnalyzer()
	];

	[Test]
	public async Task EveryRule_LinksToItsOwnPage()
	{
		foreach (DiagnosticDescriptor rule in AnalyzerDocumentationTests.Rules())
		{
			await Assert.That(rule.HelpLinkUri).EndsWith("/docs/rules/" + rule.Id + ".md");
		}
	}

	[Test]
	public async Task EveryRule_HasItsPageInTheRepository()
	{
		string rules = Path.Combine(AnalyzerDocumentationTests.RepositoryRoot(), "docs", "rules");

		foreach (DiagnosticDescriptor rule in AnalyzerDocumentationTests.Rules())
		{
			string page = Path.Combine(rules, rule.Id + ".md");

			await Assert.That(File.Exists(page)).IsTrue();
			await Assert.That(File.ReadAllText(page)).Contains("# " + rule.Id + ": " + rule.Title);
		}
	}

	[Test]
	public async Task TheIndex_ListsEveryRule()
	{
		string index = File.ReadAllText(
			Path.Combine(AnalyzerDocumentationTests.RepositoryRoot(), "docs", "rules", "README.md"));

		foreach (DiagnosticDescriptor rule in AnalyzerDocumentationTests.Rules())
		{
			await Assert.That(index).Contains("[" + rule.Id + "](" + rule.Id + ".md)");
			await Assert.That(index).Contains(rule.Title.ToString());
		}
	}

	private static IEnumerable<DiagnosticDescriptor> Rules()
	{
		return AnalyzerDocumentationTests.Analyzers.SelectMany(analyzer => analyzer.SupportedDiagnostics);
	}

	/// <summary>
	/// Walks up from the test output until the solution file is found.
	/// </summary>
	private static string RepositoryRoot()
	{
		for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null;
			 directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Chorn.AspNetCore.ProblemDetails.slnx")))
			{
				return directory.FullName;
			}
		}

		throw new InvalidOperationException("The test output is not inside the repository.");
	}
}
