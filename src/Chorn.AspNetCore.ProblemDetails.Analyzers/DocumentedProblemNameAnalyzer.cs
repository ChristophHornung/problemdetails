namespace Chorn.AspNetCore.ProblemDetails.Analyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Reports a documented problem name that the producer does not declare.
/// </summary>
/// <remarks>
/// This is the one thing the compiler cannot check: <c>nameof</c> proves a name exists, not that it exists on
/// the producer being documented. Both spellings are covered - the attribute on a controller or an action, and
/// the attribute constructed by hand as endpoint metadata on a minimal api.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentedProblemNameAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(ProblemDiagnostics.UnknownProblemName);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(start =>
		{
			if (!KnownTypes.TryGet(start.Compilation, out KnownTypes types) || types.ProducesProblems == null)
			{
				return;
			}

			start.RegisterSyntaxNodeAction(node => DocumentedProblemNameAnalyzer.AnalyzeAttribute(node, types),
				SyntaxKind.Attribute);

			start.RegisterOperationAction(operation => DocumentedProblemNameAnalyzer.AnalyzeMetadata(operation,
				types), OperationKind.ObjectCreation);
		});
	}

	private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, KnownTypes types)
	{
		AttributeSyntax attribute = (AttributeSyntax)context.Node;

		if (attribute.ArgumentList == null ||
			context.SemanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol constructor ||
			!DocumentedProblemNameAnalyzer.IsDocumentation(constructor.ContainingType, types,
				out ITypeSymbol? producer))
		{
			return;
		}

		IReadOnlyCollection<string> declared = types.ProblemNamesOf(producer!);

		foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
		{
			DocumentedProblemNameAnalyzer.Check(context.SemanticModel.GetConstantValue(argument.Expression),
				argument.Expression.GetLocation(), producer!, declared, context.ReportDiagnostic);
		}
	}

	private static void AnalyzeMetadata(OperationAnalysisContext context, KnownTypes types)
	{
		if (context.Operation is not IObjectCreationOperation creation || creation.Constructor == null ||
			!DocumentedProblemNameAnalyzer.IsDocumentation(creation.Constructor.ContainingType, types,
				out ITypeSymbol? producer))
		{
			return;
		}

		// An applied attribute is an object creation too. The syntax action already reported that one.
		if (creation.Syntax is AttributeSyntax)
		{
			return;
		}

		IReadOnlyCollection<string> declared = types.ProblemNamesOf(producer!);

		foreach (IOperation name in creation.Arguments.SelectMany(DocumentedProblemNameAnalyzer.Names))
		{
			DocumentedProblemNameAnalyzer.Check(name.ConstantValue, name.Syntax.GetLocation(), producer!,
				declared, context.ReportDiagnostic);
		}
	}

	/// <summary>
	/// Gets the individual names of an argument, which for a params array is every element of it.
	/// </summary>
	/// <param name="argument">The argument to read.</param>
	/// <returns>The operations that may be names.</returns>
	private static IEnumerable<IOperation> Names(IArgumentOperation argument)
	{
		return argument.Value is IArrayCreationOperation { Initializer: not null } array
			? array.Initializer.ElementValues
			: new[] { argument.Value };
	}

	private static bool IsDocumentation(INamedTypeSymbol? attribute, KnownTypes types, out ITypeSymbol? producer)
	{
		producer = null;

		if (attribute == null ||
			!SymbolEqualityComparer.Default.Equals(attribute.OriginalDefinition, types.ProducesProblems) ||
			attribute.TypeArguments.Length != 1)
		{
			return false;
		}

		producer = attribute.TypeArguments[0];

		// Only a producer written in this compilation can be judged. Across a reference the compiler sees its
		// public members alone, and a problem declared internal - which is how this package suggests declaring
		// them - would be reported as missing when the runtime resolves it perfectly well.
		return producer.TypeKind != TypeKind.Error && producer.DeclaringSyntaxReferences.Length > 0;
	}

	private static void Check(Optional<object?> constant, Location location, ITypeSymbol producer,
		IReadOnlyCollection<string> declared, System.Action<Diagnostic> report)
	{
		if (!constant.HasValue || constant.Value is not string name || declared.Contains(name))
		{
			return;
		}

		report(Diagnostic.Create(ProblemDiagnostics.UnknownProblemName, location, producer.Name, name,
			declared.Count == 0
				? "nothing"
				: string.Join(", ", declared.OrderBy(problem => problem, System.StringComparer.Ordinal))));
	}
}
