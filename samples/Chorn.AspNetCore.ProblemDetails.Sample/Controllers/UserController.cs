namespace Chorn.AspNetCore.ProblemDetails.Sample.Controllers;

using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Orders;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using static Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems.UserProblems;

/// <summary>
/// The user endpoints. The rules live in the services; this only decides what to answer with.
/// </summary>
/// <param name="users">The user operations.</param>
/// <param name="orders">The order operations.</param>
[ApiController]
[Route("users")]
[Produces("application/json")]
[ProducesProblems<UserProblems>(nameof(Unknown))] // Every endpoint here may miss the user, so it is said once.
public class UserController(UserService users, OrderService orders) : ControllerBase
{
	/// <summary>
	/// Assigns a user to the tenant.
	/// </summary>
	/// <param name="userId">The user to assign.</param>
	/// <returns>Nothing, once assigned.</returns>
	/// <response code="204">The user was assigned.</response>
	[HttpPost("{userId:guid}/assign")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesProblems<UserProblems>(nameof(NotSignedUp), nameof(AlreadyAssigned))]
	public IActionResult Assign(Guid userId)
	{
		// Whatever stops this is thrown from the service, layers below - the handler turns it into the response.
		users.Assign(userId, tenant: "acme");
		return this.NoContent();
	}

	/// <summary>
	/// Gets one order of one user.
	/// </summary>
	/// <param name="userId">The user the order belongs to.</param>
	/// <param name="orderId">The order to look up.</param>
	/// <returns>The order.</returns>
	/// <remarks>
	/// Both producers declare an <c>Unknown</c>. The api description keeps both, qualified by their producer,
	/// rather than letting one overwrite the other.
	/// </remarks>
	[HttpGet("{userId:guid}/orders/{orderId:int}")]
	[ProducesResponseType<Order>(StatusCodes.Status200OK)]
	[ProducesProblems<OrderProblems>(nameof(OrderProblems.Unknown))]
	public ActionResult<Order> GetOrder(Guid userId, int orderId)
	{
		return orders.Get(userId, orderId);
	}

	/// <summary>
	/// Removes a user.
	/// </summary>
	/// <param name="userId">The user to remove.</param>
	/// <returns>Nothing, once removed.</returns>
	/// <response code="204">The user was removed.</response>
	/// <response code="403">The user is the last administrator of their tenant.</response>
	[HttpDelete("{userId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesProblems<UserProblems>]
	public IActionResult Delete(Guid userId)
	{
		// Decided here rather than thrown, to show the returned form - the instance is the resource asked for.
		if (users.IsLastAdministrator(userId))
		{
			return this.Problem(LastAdministrator, instance: $"/users/{userId}");
		}

		users.Remove(userId);
		return this.NoContent();
	}
}
