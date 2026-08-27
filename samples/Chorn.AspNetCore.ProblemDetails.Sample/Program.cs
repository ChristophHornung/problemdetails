using Chorn.AspNetCore.ProblemDetails;
using Chorn.AspNetCore.ProblemDetails.OpenApi;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Orders;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Problems;
using Chorn.AspNetCore.ProblemDetails.Sample.Domain.Users;
using Chorn.AspNetCore.ProblemDetails.Swashbuckle;
using Microsoft.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<UserDirectory>();
builder.Services.AddSingleton<OrderBook>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OrderService>();

// Registers every producer of the application - the ones in the domain library included, though nothing here
// names it. It is referenced and it uses the package, which is all mvc asks of a class library with controllers.
builder.Services.AddExplainedProblemDetails();

// Both open api stacks, to show either integration. An application picks one.
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo { Title = "Problem details sample", Version = "v1" });
	options.AddExplainedProblems();
});
builder.Services.AddOpenApi(options => options.AddExplainedProblems());

WebApplication app = builder.Build();

// Without this a thrown ExplainedProblemException never reaches the handler.
app.UseExceptionHandler();

app.UseSwagger();
app.MapOpenApi();

// One ui for both documents, so the examples the two integrations write can be compared side by side.
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Swashbuckle");
	options.SwaggerEndpoint("/openapi/v1.json", "Microsoft.AspNetCore.OpenApi");
});

app.MapControllers();

// The problem types of this sample are relative uri references starting with /problems/, so the route that
// serves the explanations is the type itself - a caller can paste what they received into the address bar.
app.MapProblemExplanations("/problems/{**problemType}", problemTypePrefix: "/problems/");

if (app.Environment.IsDevelopment())
{
	// Never succeeds, so a client developer can point their error handling at something real.
	app.MapExpectedProblems("/problems/expected", problemTypeFormat: "/problems/expected/{0}");
}

// A minimal api endpoint answers with a problem exactly like a controller does.
app.MapPost("/orders/{orderId:int}/ship", (int orderId, OrderService orders) =>
	{
		orders.Ship(orderId);
		return Results.NoContent();
	})
	.WithMetadata(new ProducesProblemsAttribute<OrderProblems>(nameof(OrderProblems.AlreadyShipped),
		nameof(OrderProblems.Unknown)))
	.Produces(StatusCodes.Status204NoContent);

app.Run();

/// <summary>
/// Exposed so the tests can host this application.
/// </summary>
public partial class Program;
