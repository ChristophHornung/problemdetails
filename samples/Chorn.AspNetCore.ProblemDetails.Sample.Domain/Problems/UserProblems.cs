namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Every problem the user operations can answer with.
/// </summary>
/// <remarks>
/// Declared next to the code that throws them, in a library the web project references. Nothing registers this
/// producer: the application depends on this library and this library uses the package, which is all mvc asks
/// of a controller in a class library too. Public rather than internal only so the controllers can name the
/// problems with <c>nameof</c>.
/// </remarks>
public sealed class UserProblems : ProblemProducerBase
{
	/// <summary>
	/// Gets the problem of a user that has not completed the sign-up.
	/// </summary>
	public static ExplainedProblemDetails NotSignedUp => new()
	{
		Title = "User not yet signed up",
		Detail = "The user cannot be assigned because they have not signed up yet.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/user/not-signed-up",
		Explanation =
			"A user has to sign up before they can be assigned to a tenant. Have them complete the sign-up " +
			"flow, then repeat this request."
	};

	/// <summary>
	/// Gets the problem of a user that already belongs to a tenant.
	/// </summary>
	public static ExplainedProblemDetails AlreadyAssigned => new()
	{
		Title = "User is already assigned",
		Detail = "Assigning a user to more than one tenant is not supported.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/user/already-assigned",
		Explanation =
			"Remove the user from their current tenant before assigning them to another one. A user belongs to " +
			"exactly one tenant at a time."
	};

	/// <summary>
	/// Gets the problem of a user id that belongs to nobody.
	/// </summary>
	public static ExplainedProblemDetails Unknown => new()
	{
		Title = "User not found",
		Detail = "No user exists for the given id.",
		Status = StatusCodes.Status404NotFound,
		Type = "/problems/user/unknown",
		Explanation =
			"The id belongs to no user this tenant can see. Note that OrderProblems declares an Unknown of its " +
			"own - an endpoint documenting both gets one example per producer."
	};

	/// <summary>
	/// Gets the problem of removing the one administrator a tenant has left.
	/// </summary>
	public static ExplainedProblemDetails LastAdministrator => new()
	{
		Title = "User is the last administrator",
		Detail = "The last administrator of a tenant cannot be removed.",
		Status = StatusCodes.Status403Forbidden,
		Type = "/problems/user/last-administrator",
		// A member the problem itself declares. It belongs in the response and in the api example alike.
		Extensions = { ["policy"] = "last-administrator" },
		Explanation =
			"Promote another user to administrator first. A tenant without an administrator can no longer be " +
			"managed by anyone but support."
	};
}
