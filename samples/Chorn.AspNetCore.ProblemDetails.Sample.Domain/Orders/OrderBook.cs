namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Orders;

using System.Collections.Concurrent;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;

/// <summary>
/// The orders, held in memory and seeded.
/// </summary>
public sealed class OrderBook
{
	private readonly ConcurrentDictionary<int, Order> orders =
		new(OrderBook.Seed().ToDictionary(order => order.Id));

	/// <summary>
	/// Finds an order.
	/// </summary>
	/// <param name="id">The id to look for.</param>
	/// <returns>The order, or <c>null</c> when there is none.</returns>
	public Order? Find(int id)
	{
		return this.orders.GetValueOrDefault(id);
	}

	private static IEnumerable<Order> Seed()
	{
		return
		[
			new Order { Id = 1, OwnerId = UserDirectory.Known.Ada, Description = "A difference engine", IsShipped = true },
			new Order { Id = 2, OwnerId = UserDirectory.Known.Ada, Description = "An analytical engine" },
			new Order { Id = 3, OwnerId = UserDirectory.Known.Bob, Description = "A slide rule" }
		];
	}
}
