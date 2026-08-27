namespace Chorn.AspNetCore.ProblemDetails;

using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// A problem detail together with a full explanation written for the developer that receives it.
/// </summary>
/// <remarks>
/// The <see cref="Explanation"/> is meant for the api description and the endpoint
/// <see cref="ProblemExplanationEndpointRouteBuilderExtensions.MapProblemExplanations"/> maps, not for a response
/// body. <c>AsException</c> and the <c>Problem</c> extension keep it out by copying the other members; handing
/// the instance itself to a framework api such as <c>Results.Problem</c> serializes it whole, which the CHPD001
/// analyzer rule reports.
/// </remarks>
public class ExplainedProblemDetails : MvcProblemDetails
{
	/// <summary>
	/// Gets or sets the full explanation of the problem.
	/// </summary>
	public string? Explanation { get; set; }
}
