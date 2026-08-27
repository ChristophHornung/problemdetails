namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Orders;

using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;

/// <summary>
/// What can be done with an order, and what stops it.
/// </summary>
/// <param name="orders">The orders.</param>
/// <param name="users">The users.</param>
public sealed class OrderService(OrderBook orders, UserDirectory users)
{
	/// <summary>
	/// Gets one order of one user.
	/// </summary>
	/// <param name="userId">The user.</param>
	/// <param name="orderId">The order.</param>
	/// <returns>The order.</returns>
	/// <exception cref="ExplainedProblemException">if the user is unknown, or the order is not theirs.</exception>
	public Order Get(Guid userId, int orderId)
	{
		if (users.Find(userId) == null)
		{
			throw UserProblems.Unknown.AsException(new Dictionary<string, object?> { ["userId"] = userId });
		}

		Order? order = orders.Find(orderId);

		// Somebody else's order is as good as no order, from where this user stands.
		return order != null && order.OwnerId == userId ? order : throw OrderService.Unknown(orderId);
	}

	/// <summary>
	/// Ships an order.
	/// </summary>
	/// <param name="orderId">The order.</param>
	/// <exception cref="ExplainedProblemException">if the order is unknown or already shipped.</exception>
	public void Ship(int orderId)
	{
		Order order = orders.Find(orderId) ?? throw OrderService.Unknown(orderId);

		if (order.IsShipped)
		{
			throw OrderProblems.AlreadyShipped.AsException(new Dictionary<string, object?> { ["orderId"] = orderId });
		}

		order.IsShipped = true;
	}

	private static ExplainedProblemException Unknown(int orderId)
	{
		return OrderProblems.Unknown.AsException(new Dictionary<string, object?> { ["orderId"] = orderId });
	}
}
