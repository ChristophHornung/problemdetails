namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;

using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;

/// <summary>
/// What can be done with a user - and, several layers below any controller, what stops it.
/// </summary>
/// <param name="directory">The users.</param>
public sealed class UserService(UserDirectory directory)
{
	/// <summary>
	/// Assigns a user to a tenant.
	/// </summary>
	/// <param name="userId">The user.</param>
	/// <param name="tenant">The tenant.</param>
	/// <exception cref="ExplainedProblemException">
	/// if the user is unknown, has not signed up, or already belongs to a tenant.
	/// </exception>
	public void Assign(Guid userId, string tenant)
	{
		User user = this.Require(userId);

		// Thrown, not returned: there is no HttpContext down here, and there does not need to be.
		if (!user.IsSignedUp)
		{
			throw UserProblems.NotSignedUp.AsException(UserService.About(userId));
		}

		if (user.Tenant != null)
		{
			throw UserProblems.AlreadyAssigned.AsException(UserService.About(userId));
		}

		user.Tenant = tenant;
	}

	/// <summary>
	/// Gets a value indicating whether removing the user would leave their tenant without an administrator.
	/// </summary>
	/// <param name="userId">The user.</param>
	/// <returns><c>true</c> if they are the last administrator.</returns>
	public bool IsLastAdministrator(Guid userId)
	{
		return directory.Find(userId) is { IsAdministrator: true, Tenant: { } tenant } &&
		       directory.Administrators(tenant).Count() == 1;
	}

	/// <summary>
	/// Removes a user.
	/// </summary>
	/// <param name="userId">The user.</param>
	/// <exception cref="ExplainedProblemException">if the user is unknown.</exception>
	public void Remove(Guid userId)
	{
		this.Require(userId);
		directory.Remove(userId);
	}

	private User Require(Guid userId)
	{
		return directory.Find(userId) ?? throw UserProblems.Unknown.AsException(UserService.About(userId));
	}

	private static Dictionary<string, object?> About(Guid userId)
	{
		return new Dictionary<string, object?> { ["userId"] = userId };
	}
}
