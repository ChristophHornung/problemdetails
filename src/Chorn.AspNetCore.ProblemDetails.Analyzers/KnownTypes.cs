namespace Chorn.AspNetCore.ProblemDetails.Analyzers;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

/// <summary>
/// The types the analyzers recognise, looked up once per compilation.
/// </summary>
internal sealed class KnownTypes
{
	private KnownTypes(INamedTypeSymbol explainedProblemDetails, INamedTypeSymbol problemProducer,
		INamedTypeSymbol? mvcProblemDetails, INamedTypeSymbol? producesProblems, INamedTypeSymbol? controllerBase,
		INamedTypeSymbol? nonAction)
	{
		this.ExplainedProblemDetails = explainedProblemDetails;
		this.ProblemProducer = problemProducer;
		this.MvcProblemDetails = mvcProblemDetails;
		this.ProducesProblems = producesProblems;
		this.ControllerBase = controllerBase;
		this.NonAction = nonAction;
	}

	/// <summary>
	/// Gets the problem type every declared problem is.
	/// </summary>
	public INamedTypeSymbol ExplainedProblemDetails { get; }

	/// <summary>
	/// Gets the interface every producer implements.
	/// </summary>
	public INamedTypeSymbol ProblemProducer { get; }

	/// <summary>
	/// Gets the framework problem details, or <c>null</c> when it is not referenced.
	/// </summary>
	public INamedTypeSymbol? MvcProblemDetails { get; }

	/// <summary>
	/// Gets the open generic documentation attribute.
	/// </summary>
	public INamedTypeSymbol? ProducesProblems { get; }

	/// <summary>
	/// Gets the controller base type, or <c>null</c> when mvc is not referenced.
	/// </summary>
	public INamedTypeSymbol? ControllerBase { get; }

	/// <summary>
	/// Gets the attribute that says a public controller method is not an endpoint.
	/// </summary>
	public INamedTypeSymbol? NonAction { get; }

	/// <summary>
	/// Looks the types up in the compilation being analyzed.
	/// </summary>
	/// <param name="compilation">The compilation.</param>
	/// <param name="types">The types, when this package is referenced at all.</param>
	/// <returns><c>true</c> if there is anything to analyze.</returns>
	public static bool TryGet(Compilation compilation, out KnownTypes types)
	{
		INamedTypeSymbol? problem =
			compilation.GetTypeByMetadataName("Chorn.AspNetCore.ProblemDetails.ExplainedProblemDetails");
		INamedTypeSymbol? producer =
			compilation.GetTypeByMetadataName("Chorn.AspNetCore.ProblemDetails.IProblemProducer");

		if (problem == null || producer == null)
		{
			types = null!;
			return false;
		}

		types = new KnownTypes(problem, producer,
			compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ProblemDetails"),
			compilation.GetTypeByMetadataName("Chorn.AspNetCore.ProblemDetails.ProducesProblemsAttribute`1"),
			compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase"),
			compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute"));

		return true;
	}

	/// <summary>
	/// Gets a value indicating whether the given type is a declared problem.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns><c>true</c> if it is an explained problem.</returns>
	public bool IsProblem(ITypeSymbol? type)
	{
		for (ITypeSymbol? candidate = type; candidate != null; candidate = candidate.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(candidate, this.ExplainedProblemDetails))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets a value indicating whether the given type produces problems.
	/// </summary>
	/// <param name="type">The type to check.</param>
	/// <returns><c>true</c> if it is a producer.</returns>
	public bool IsProducer(ITypeSymbol? type)
	{
		return type != null && type.TypeKind == TypeKind.Class &&
			   type.AllInterfaces.Any(@interface =>
				   SymbolEqualityComparer.Default.Equals(@interface, this.ProblemProducer));
	}

	/// <summary>
	/// Gets the names of every problem the given producer declares, including inherited ones.
	/// </summary>
	/// <param name="producer">The producer to read.</param>
	/// <returns>The declared names.</returns>
	public IReadOnlyCollection<string> ProblemNamesOf(ITypeSymbol producer)
	{
		HashSet<string> names = new(System.StringComparer.Ordinal);

		for (ITypeSymbol? declaring = producer; declaring != null; declaring = declaring.BaseType)
		{
			foreach (IPropertySymbol property in declaring.GetMembers().OfType<IPropertySymbol>())
			{
				if (property.IsStatic && !property.IsIndexer && property.GetMethod != null &&
				    this.IsProblem(property.Type))
				{
					names.Add(property.Name);
				}
			}
		}

		return names;
	}
}
