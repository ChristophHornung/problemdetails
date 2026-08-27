namespace Chorn.AspNetCore.ProblemDetails.Analyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Reports a declared problem handed to something that serializes it whole.
/// </summary>
/// <remarks>
/// An explained problem is a problem details, so every framework api takes one - and every one of them
/// serializes the runtime type, which carries the explanation. The two ways that do not are AsException and
/// Problem, both of which copy the members a caller is meant to see.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExplanationLeakAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// The apis that serialize whatever they are given.
	/// </summary>
	private static readonly HashSet<string> Sinks = new(System.StringComparer.Ordinal)
	{
		"Microsoft.AspNetCore.Http.Results",
		"Microsoft.AspNetCore.Http.TypedResults",
		"Microsoft.AspNetCore.Mvc.ControllerBase",
		"Microsoft.AspNetCore.Mvc.ObjectResult",
		"Microsoft.AspNetCore.Mvc.JsonResult",
		"Microsoft.AspNetCore.Http.HttpResponseJsonExtensions",
		"System.Text.Json.JsonSerializer"
	};

	/// <summary>
	/// The wrappers an action returns a problem inside, which are unwrapped before it is judged.
	/// </summary>
	private static readonly HashSet<string> Wrappers = new(System.StringComparer.Ordinal)
	{
		"System.Threading.Tasks.Task<TResult>",
		"System.Threading.Tasks.ValueTask<TResult>",
		"Microsoft.AspNetCore.Mvc.ActionResult<TValue>"
	};

	/// <inheritdoc />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(ProblemDiagnostics.ExplanationLeak);

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

			start.RegisterOperationAction(operation => ExplanationLeakAnalyzer.AnalyzeCall(operation, types),
				OperationKind.Invocation, OperationKind.ObjectCreation);

			start.RegisterSymbolAction(symbol => ExplanationLeakAnalyzer.AnalyzeAction(symbol, types),
				SymbolKind.Method);
		});
	}

	private static void AnalyzeCall(OperationAnalysisContext context, KnownTypes types)
	{
		IMethodSymbol? target;
		ImmutableArray<IArgumentOperation> arguments;
		string called;

		if (context.Operation is IInvocationOperation invocation)
		{
			target = invocation.TargetMethod;
			arguments = invocation.Arguments;
			called = target.ContainingType.Name + "." + target.Name;
		}
		else if (context.Operation is IObjectCreationOperation creation && creation.Constructor != null)
		{
			target = creation.Constructor;
			arguments = creation.Arguments;
			called = "new " + target.ContainingType.Name;
		}
		else
		{
			return;
		}

		// Matched up the hierarchy, because OkObjectResult and its siblings all serialize like ObjectResult.
		if (!ExplanationLeakAnalyzer.IsSink(target.ContainingType))
		{
			return;
		}

		foreach (IArgumentOperation argument in arguments)
		{
			ExplanationLeakAnalyzer.AnalyzeArgument(context, types, argument, called);
		}
	}

	private static void AnalyzeArgument(OperationAnalysisContext context, KnownTypes types,
		IArgumentOperation argument, string called)
	{
		IOperation value = argument.Value;
		while (value is IConversionOperation conversion)
		{
			value = conversion.Operand;
		}

		if (argument.Parameter == null || !types.IsProblem(value.Type))
		{
			return;
		}

		// The declared parameter type is what decides: a problem handed to something taking object, a type
		// parameter or a plain ProblemDetails is serialized by its runtime type, explanation and all.
		ITypeSymbol declared = argument.Parameter.OriginalDefinition.Type;
		bool serializesAnything = declared.TypeKind == TypeKind.TypeParameter ||
								  declared.SpecialType == SpecialType.System_Object ||
								  SymbolEqualityComparer.Default.Equals(declared, types.MvcProblemDetails);

		if (serializesAnything)
		{
			context.ReportDiagnostic(Diagnostic.Create(ProblemDiagnostics.ExplanationLeak,
				value.Syntax.GetLocation(), called));
		}
	}

	private static void AnalyzeAction(SymbolAnalysisContext context, KnownTypes types)
	{
		if (types.ControllerBase == null || context.Symbol is not IMethodSymbol method ||
			method is not { MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public,
				IsStatic: false, IsGenericMethod: false })
		{
			return;
		}

		// A public method that is not an endpoint serializes nothing.
		if (types.NonAction != null && method.GetAttributes().Any(attribute =>
			    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, types.NonAction)))
		{
			return;
		}

		if (!types.IsProblem(ExplanationLeakAnalyzer.Unwrap(method.ReturnType)) ||
			!ExplanationLeakAnalyzer.IsController(method.ContainingType, types))
		{
			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(ProblemDiagnostics.ExplanationLeak,
			method.Locations[0], method.ContainingType.Name + "." + method.Name));
	}

	/// <summary>
	/// Gets a value indicating whether calls on the given type serialize whatever they are handed.
	/// </summary>
	private static bool IsSink(INamedTypeSymbol? type)
	{
		for (INamedTypeSymbol? candidate = type; candidate != null; candidate = candidate.BaseType)
		{
			if (ExplanationLeakAnalyzer.Sinks.Contains(candidate.ToDisplayString()))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets what an action really answers with, looking through the wrappers it may return it inside.
	/// </summary>
	/// <param name="returnType">The declared return type.</param>
	/// <returns>The type that ends up being serialized.</returns>
	private static ITypeSymbol Unwrap(ITypeSymbol returnType)
	{
		ITypeSymbol current = returnType;

		while (current is INamedTypeSymbol { Arity: 1 } wrapper &&
		       ExplanationLeakAnalyzer.Wrappers.Contains(wrapper.OriginalDefinition.ToDisplayString()))
		{
			current = wrapper.TypeArguments[0];
		}

		return current;
	}

	private static bool IsController(ITypeSymbol? type, KnownTypes types)
	{
		for (ITypeSymbol? candidate = type; candidate != null; candidate = candidate.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(candidate, types.ControllerBase))
			{
				return true;
			}
		}

		return false;
	}
}
