namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Every problem the order operations can answer with.
/// </summary>
public sealed class OrderProblems : ProblemProducerBase
{
	/// <summary>
	/// Gets the problem of shipping an order twice.
	/// </summary>
	public static ExplainedProblemDetails AlreadyShipped => new()
	{
		Title = "Order already shipped",
		Detail = "The order has already been shipped and cannot be shipped again.",
		Status = StatusCodes.Status409Conflict,
		Type = "/problems/order/already-shipped",
		Explanation =
			"Shipping is final. To correct a shipment, cancel it first - the order then returns to the state it " +
			"was in before."
	};

	/// <summary>
	/// Gets the problem of an order id that belongs to nobody, or not to this user.
	/// </summary>
	public static ExplainedProblemDetails Unknown => new()
	{
		Title = "Order not found",
		Detail = "No order exists for the given id.",
		Status = StatusCodes.Status404NotFound,
		Type = "/problems/order/unknown",
		Explanation =
			"The id belongs to no order this user can see. Check that the id is complete and that the order was " +
			"not placed by another user."
	};
}
