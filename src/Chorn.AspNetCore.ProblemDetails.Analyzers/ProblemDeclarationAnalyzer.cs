namespace Chorn.AspNetCore.ProblemDetails.Analyzers;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Reports the ways a producer can be written that the registration cannot make sense of.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProblemDeclarationAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(ProblemDiagnostics.SharedProblemDeclaration, ProblemDiagnostics.GenericProducer,
			ProblemDiagnostics.FieldProblemDeclaration);

	/// <inheritdoc />
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(start =>
		{
			if (!KnownTypes.TryGet(start.Compilation, out KnownTypes types))
			{
				return;
			}

			start.RegisterSymbolAction(symbol => ProblemDeclarationAnalyzer.AnalyzeProducer(symbol, types),
				SymbolKind.NamedType);
			start.RegisterSymbolAction(symbol => ProblemDeclarationAnalyzer.AnalyzeDeclaration(symbol, types),
				SymbolKind.Property, SymbolKind.Field);
		});
	}

	/// <summary>
	/// Reports a producer the container could never create.
	/// </summary>
	private static void AnalyzeProducer(SymbolAnalysisContext context, KnownTypes types)
	{
		if (context.Symbol is INamedTypeSymbol { IsAbstract: false } producer && types.IsProducer(producer) &&
			ProblemDeclarationAnalyzer.IsOpenGeneric(producer))
		{
			context.ReportDiagnostic(Diagnostic.Create(ProblemDiagnostics.GenericProducer,
				producer.Locations[0], producer.Name));
		}
	}

	/// <summary>
	/// Gets a value indicating whether the type still carries type parameters of its own or of a type it is
	/// nested in, which is what the registration checks before it skips one.
	/// </summary>
	/// <param name="producer">The producer to check.</param>
	/// <returns><c>true</c> if the container could not create it.</returns>
	private static bool IsOpenGeneric(INamedTypeSymbol producer)
	{
		for (INamedTypeSymbol? declaring = producer; declaring != null; declaring = declaring.ContainingType)
		{
			if (declaring.Arity > 0)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Reports a problem that is created once and shared, or one that is not declared as a problem at all.
	/// </summary>
	private static void AnalyzeDeclaration(SymbolAnalysisContext context, KnownTypes types)
	{
		ITypeSymbol? declaredType = context.Symbol switch
		{
			IPropertySymbol property => property.Type,
			IFieldSymbol field => field.Type,
			_ => null
		};

		if (!context.Symbol.IsStatic || !types.IsProblem(declaredType) ||
			!types.IsProducer(context.Symbol.ContainingType))
		{
			return;
		}

		if (context.Symbol is IFieldSymbol { IsImplicitlyDeclared: false })
		{
			// Only properties are read, so a field is not a shared problem - it is an invisible one.
			context.ReportDiagnostic(Diagnostic.Create(ProblemDiagnostics.FieldProblemDeclaration,
				context.Symbol.Locations[0], context.Symbol.Name));
			return;
		}

		if (ProblemDeclarationAnalyzer.IsCreatedOnce(context.Symbol))
		{
			context.ReportDiagnostic(Diagnostic.Create(ProblemDiagnostics.SharedProblemDeclaration,
				context.Symbol.Locations[0], context.Symbol.Name));
		}
	}

	/// <summary>
	/// Gets a value indicating whether the declaration builds its problem once rather than per read.
	/// </summary>
	/// <param name="symbol">The property declaring the problem.</param>
	/// <returns><c>true</c> if every reader would get the same instance.</returns>
	private static bool IsCreatedOnce(ISymbol symbol)
	{
		// A property with an initializer runs it once; an expression body runs on every read.
		return symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is
			PropertyDeclarationSyntax { Initializer: not null };
	}
}
