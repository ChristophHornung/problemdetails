namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;

using System.Collections.Concurrent;

/// <summary>
/// The users, held in memory and seeded so every path through the api has somebody to take it.
/// </summary>
public sealed class UserDirectory
{
	private readonly ConcurrentDictionary<Guid, User> users =
		new(UserDirectory.Seed().ToDictionary(user => user.Id));

	/// <summary>
	/// Finds a user.
	/// </summary>
	/// <param name="id">The id to look for.</param>
	/// <returns>The user, or <c>null</c> when there is none.</returns>
	public User? Find(Guid id)
	{
		return this.users.GetValueOrDefault(id);
	}

	/// <summary>
	/// Removes a user.
	/// </summary>
	/// <param name="id">The id to remove.</param>
	public void Remove(Guid id)
	{
		this.users.TryRemove(id, out _);
	}

	/// <summary>
	/// Gets the administrators of a tenant.
	/// </summary>
	/// <param name="tenant">The tenant.</param>
	/// <returns>Its administrators.</returns>
	public IEnumerable<User> Administrators(string tenant)
	{
		return this.users.Values.Where(user => user.IsAdministrator && user.Tenant == tenant);
	}

	private static IEnumerable<User> Seed()
	{
		return
		[
			new User { Id = Known.Ada, Name = "Ada", IsSignedUp = true, Tenant = "acme", IsAdministrator = true },
			new User { Id = Known.Bob, Name = "Bob", IsSignedUp = true, Tenant = "acme" },
			new User { Id = Known.Cleo, Name = "Cleo", IsSignedUp = true },
			new User { Id = Known.Dan, Name = "Dan" },
			new User { Id = Known.Eve, Name = "Eve", IsSignedUp = true, Tenant = "acme" }
		];
	}

	/// <summary>
	/// The seeded users, each there to take one path through the api.
	/// </summary>
	public static class Known
	{
		/// <summary>The only administrator of acme, so she cannot be removed.</summary>
		public static readonly Guid Ada = new("00000000-0000-0000-0000-000000000001");

		/// <summary>A member of acme, so he cannot be assigned again.</summary>
		public static readonly Guid Bob = new("00000000-0000-0000-0000-000000000002");

		/// <summary>Signed up and in no tenant yet, so she can be assigned.</summary>
		public static readonly Guid Cleo = new("00000000-0000-0000-0000-000000000003");

		/// <summary>Not signed up, so he cannot be assigned.</summary>
		public static readonly Guid Dan = new("00000000-0000-0000-0000-000000000004");

		/// <summary>A member of acme who is not its administrator, so she can be removed.</summary>
		public static readonly Guid Eve = new("00000000-0000-0000-0000-000000000005");
	}
}
