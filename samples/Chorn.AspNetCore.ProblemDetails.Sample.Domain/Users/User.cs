namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;

/// <summary>
/// Somebody who may sign up, join a tenant and administer it.
/// </summary>
public sealed class User
{
	/// <summary>
	/// Gets the id.
	/// </summary>
	public required Guid Id { get; init; }

	/// <summary>
	/// Gets the name.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets a value indicating whether the user has completed the sign-up.
	/// </summary>
	public bool IsSignedUp { get; init; }

	/// <summary>
	/// Gets a value indicating whether the user administers their tenant.
	/// </summary>
	public bool IsAdministrator { get; init; }

	/// <summary>
	/// Gets or sets the tenant the user belongs to, if any.
	/// </summary>
	public string? Tenant { get; set; }
}
