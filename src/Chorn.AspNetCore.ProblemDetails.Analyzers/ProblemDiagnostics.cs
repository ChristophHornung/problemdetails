namespace Chorn.AspNetCore.ProblemDetails.Analyzers;

using Microsoft.CodeAnalysis;

/// <summary>
/// The diagnostics this package reports.
/// </summary>
internal static class ProblemDiagnostics
{
	/// <summary>
	/// The category every diagnostic here belongs to.
	/// </summary>
	private const string Category = "Chorn.ProblemDetails";

	/// <summary>
	/// Gets the page documenting a rule, which the ide shows the rule id as a link to.
	/// </summary>
	/// <param name="rule">The rule id.</param>
	/// <returns>The page.</returns>
	private static string HelpLink(string rule)
	{
		return "https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/" + rule + ".md";
	}

	/// <summary>
	/// An explained problem handed to something that serializes it whole, explanation included.
	/// </summary>
	public static readonly DiagnosticDescriptor ExplanationLeak = new(
		"CHPD001",
		"The explanation would be sent to the caller",
		"{0} serializes the problem by its runtime type, so its Explanation ends up in the response body. " +
		"Throw it with AsException() or return it with Problem() instead.",
		ProblemDiagnostics.Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description:
		"ExplainedProblemDetails derives from ProblemDetails, so every framework api accepts it - and every " +
		"one of them serializes the runtime type. The Explanation is written for a developer reading the api " +
		"description, not for a response body, and may say more about the service than a caller should see.",
		helpLinkUri: ProblemDiagnostics.HelpLink("CHPD001"));

	/// <summary>
	/// A documented problem name that the producer does not declare.
	/// </summary>
	public static readonly DiagnosticDescriptor UnknownProblemName = new(
		"CHPD002",
		"The producer declares no problem of that name",
		"{0} declares no problem named '{1}'. It declares: {2}.",
		ProblemDiagnostics.Category,
		DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description:
		"nameof only proves the name exists somewhere - typically on a different producer than the one being " +
		"documented. A name that does not resolve would otherwise drop the problem out of the api " +
		"description, or fail the application at startup.",
		helpLinkUri: ProblemDiagnostics.HelpLink("CHPD002"));

	/// <summary>
	/// A problem declared once and shared by every request.
	/// </summary>
	public static readonly DiagnosticDescriptor SharedProblemDeclaration = new(
		"CHPD003",
		"The problem is declared as a single shared instance",
		"Declare '{0}' as an expression body, so every reader gets its own problem",
		ProblemDiagnostics.Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description:
		"A problem that is created once is shared by every request that reads it. Adding a per-request " +
		"extension to it would then reach other requests, and two requests doing so at the same time can " +
		"corrupt the extensions dictionary.",
		helpLinkUri: ProblemDiagnostics.HelpLink("CHPD003"));

	/// <summary>
	/// A producer the registration cannot use.
	/// </summary>
	public static readonly DiagnosticDescriptor GenericProducer = new(
		"CHPD004",
		"A generic producer is never registered",
		"'{0}' is generic, so the registration skips it and none of its problems exist at runtime",
		ProblemDiagnostics.Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description:
		"The container cannot create an open generic implementation type, so a generic producer is skipped " +
		"when the assembly is scanned. Its problems would silently be missing from the storage and from the " +
		"api description.",
		helpLinkUri: ProblemDiagnostics.HelpLink("CHPD004"));

	/// <summary>
	/// A problem declared as a field, which is not a declaration at all.
	/// </summary>
	public static readonly DiagnosticDescriptor FieldProblemDeclaration = new(
		"CHPD005",
		"The problem is declared as a field",
		"'{0}' is a field, so it is not read as a problem at all. Declare it as a static property.",
		ProblemDiagnostics.Category,
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description:
		"Only static properties are read as problems. A field of the same type looks like a declaration, is " +
		"never found, and any endpoint documenting it by name fails to resolve.",
		helpLinkUri: ProblemDiagnostics.HelpLink("CHPD005"));
}
