namespace Chorn.AspNetCore.ProblemDetails.Sample.Domain.Orders;

/// <summary>
/// Something a user ordered.
/// </summary>
public sealed class Order
{
	/// <summary>
	/// Gets the id.
	/// </summary>
	public required int Id { get; init; }

	/// <summary>
	/// Gets the user who placed it.
	/// </summary>
	public required Guid OwnerId { get; init; }

	/// <summary>
	/// Gets what was ordered.
	/// </summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets or sets a value indicating whether it has left the building.
	/// </summary>
	public bool IsShipped { get; set; }
}
