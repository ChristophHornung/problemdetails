# Chorn.AspNetCore.ProblemDetails

This library allows you to:

* declare each problem your api can answer with once - that one declaration is what you throw,
  what your open api document shows and what a client developer looks up, so they cannot drift apart;
* throw a problem from deep in your domain code, with no `HttpContext` or controller in sight, and have it come
  out as a correct `application/problem+json` response;
* put a real example body for every problem on every endpoint of your open api document, explanation included,
  instead of a bare `409 Conflict`;
* give every problem a `type` a developer can open and read what went wrong and what to do about it.

| Package | NuGet | What it adds |
|---|---|---|
| `Chorn.AspNetCore.ProblemDetails` | [![NuGet](https://img.shields.io/nuget/v/Chorn.AspNetCore.ProblemDetails.svg?style=flat-square)](https://www.nuget.org/packages/Chorn.AspNetCore.ProblemDetails/) | Declaring, throwing and looking up problems. No third party dependencies. |
| `Chorn.AspNetCore.ProblemDetails.Swashbuckle` | [![NuGet](https://img.shields.io/nuget/v/Chorn.AspNetCore.ProblemDetails.Swashbuckle.svg?style=flat-square)](https://www.nuget.org/packages/Chorn.AspNetCore.ProblemDetails.Swashbuckle/) | The problems as `application/problem+json` examples in a Swashbuckle document. |
| `Chorn.AspNetCore.ProblemDetails.OpenApi` | [![NuGet](https://img.shields.io/nuget/v/Chorn.AspNetCore.ProblemDetails.OpenApi.svg?style=flat-square)](https://www.nuget.org/packages/Chorn.AspNetCore.ProblemDetails.OpenApi/) | The same, for the built-in `Microsoft.AspNetCore.OpenApi` document. |

A problem is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem details plus that explanation, declared
as a static property on a producer:

```csharp
internal sealed class SigningProblems : ProblemProducerBase
{
	internal static ExplainedProblemDetails SignatureMismatch => new()
	{
		Title = "Request signature does not match",
		Detail = "The X-Signature header does not match the body of the request.",
		Status = StatusCodes.Status401Unauthorized,
		Type = "/problems/signing/signature-mismatch",
		Explanation =
			"The signature is an HMAC-SHA256 over the raw request bytes, exactly as they are sent. The usual " +
			"cause is an http client that serializes the body a second time after signing it - key order or " +
			"whitespace change, and the hash with them. Sign the bytes you actually send, and sign them with the " +
			"endpoint secret from the settings page, not with the api key."
	};
}
```

`Detail` is what the caller sees in the response. `Explanation` is what they read when they follow the `type`.

## Install

```shell
dotnet add package Chorn.AspNetCore.ProblemDetails
```

plus the integration for the open api stack you use, if any:

```shell
dotnet add package Chorn.AspNetCore.ProblemDetails.Swashbuckle
dotnet add package Chorn.AspNetCore.ProblemDetails.OpenApi
```

Targets `net10.0`.

## Wiring it up

```csharp
builder.Services.AddExplainedProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();       // routes a thrown ExplainedProblemException to the handler
app.MapProblemExplanations();    // optional: the explanation for a type a caller received
```

`AddExplainedProblemDetails` registers every producer in your application, the exception handler and the
framework `AddProblemDetails` services. A new producer needs no wiring - it exists, so it is registered.

"Your application" means what it means to MVC looking for controllers: the assembly the host is named after plus
every dependency of it that references this package. So the call may sit in a shared `ServiceDefaults`-style
library, and a producer in a domain library is found like a controller in a class library. Only the libraries
the dependency graph says use this package are loaded.

To name things yourself - or because you publish single-file or trimmed, where there is no dependency graph to
read - scan an assembly explicitly:

```csharp
builder.Services.AddExplainedProblemDetails(options => options.ScanAssemblyOf<UserProblems>());
```

Finding no producer at all throws, listing where it looked, rather than registering nothing.

## Answering with a problem

From anywhere - a domain service, a repository, a validator:

```csharp
throw UserProblems.NotSignedUp.AsException();

// with members of this request on the response body:
throw UserProblems.NotSignedUp.AsException(new Dictionary<string, object?> { ["userId"] = userId });
```

Or from a controller, if you are already there:

```csharp
return this.Problem(UserProblems.NotSignedUp, instance: $"/users/{userId}");
```

Either way the caller receives an ordinary problem response:

```json
{
  "type": "/problems/user/not-signed-up",
  "title": "User not yet signed up",
  "status": 409,
  "detail": "The user cannot be assigned because they have not signed up yet.",
  "userId": "00000000-0000-0000-0000-000000000004",
  "traceId": "00-8f2b...-01"
}
```

The `Explanation` is not in it. It is written for a developer reading documentation, and reaches them through
the api description and the explanation endpoint instead.

## Documenting problems

Name the problems an endpoint can answer with, and they become part of its api description:

```csharp
using static MyApi.Problems.UserProblems;    // keeps the names short

[ProducesProblems<UserProblems>(nameof(Unknown))]    // on the controller: every endpoint in it
public class UserController : ControllerBase
{
	[HttpPost("{userId:guid}/assign")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesProblems<UserProblems>(nameof(NotSignedUp), nameof(AlreadyAssigned))]
	public IActionResult Assign(Guid userId) { ... }
}
```

On a minimal api the attribute is endpoint metadata:

```csharp
app.MapPost("/orders/{orderId:int}/ship", ShipOrder)
   .WithMetadata(new ProducesProblemsAttribute<OrderProblems>(nameof(OrderProblems.AlreadyShipped)));
```

Register the integration you use:

```csharp
builder.Services.AddSwaggerGen(options => options.AddExplainedProblems());   // Swashbuckle
builder.Services.AddOpenApi(options => options.AddExplainedProblems());      // Microsoft.AspNetCore.OpenApi
```

Each problem becomes an example of the `application/problem+json` response for its status code, keyed by its
name, summarised by its type and described by its `Explanation`:

```jsonc
"409": {
  "description": "Conflict",
  "content": {
    "application/problem+json": {
      "schema": { "$ref": "#/components/schemas/ProblemDetails" },
      "examples": {
        "NotSignedUp": {
          "summary": "/problems/user/not-signed-up",
          "description": "A user has to sign up before they can be assigned to a tenant. Have them complete ...",
          "value": {
            "type": "/problems/user/not-signed-up",
            "title": "User not yet signed up",
            "status": 409,
            "detail": "The user cannot be assigned because they have not signed up yet."
          }
        }
      }
    }
  }
}
```

* A status code only a problem produces needs no `[ProducesResponseType]` of its own; one the endpoint declares
  keeps its `<response>` description.
* The attribute may be repeated to document problems from more than one producer. Two producers sharing a name
  are told apart in the example keys - `UserProblems.Unknown`, `OrderProblems.Unknown`.
* Naming no problem - `[ProducesProblems<UserProblems>]` - documents every problem the producer declares.
* A `nameof` proves the name exists, not that it exists on that producer. **CHPD002** catches the difference at
  compile time; the startup resolves the names again as a backstop.

## The explanation endpoint

`MapProblemExplanations` maps the other half of a problem response: the `type` a caller received goes in, the
`Explanation` comes out. Shape the types like the route, and each one is the address of its own explanation -
RFC 9457 allows a relative uri reference:

```csharp
app.MapProblemExplanations("/problems/{**problemType}", problemTypePrefix: "/problems/");
```

```
GET /problems/user/not-signed-up   the problem, this time with its explanation
GET /problems                      every problem the application can produce
GET /problems/user/nope            404 - itself a problem response
```

`problemTypePrefix` is whatever the route leaves out; without arguments the route is `/problem/{**problemType}`
and the captured path is the whole type. The endpoint is anonymous on purpose - a caller that just failed to
authenticate is exactly who needs it - and unnamed, so it can be mapped under two api versions; chain
`.RequireAuthorization()` or pass `endpointName` to change either.

`MapExpectedProblems` maps endpoints that never succeed, for client developers to point their error handling at:

```csharp
if (app.Environment.IsDevelopment())
{
	// /problems/expected answers with a 500, /problems/expected/418 with a 418
	app.MapExpectedProblems("/problems/expected", problemTypeFormat: "/problems/expected/{0}");
}
```

## The analyzer

The core package ships five Roslyn rules; referencing it is enough. They catch what neither the type system nor
the startup can, and an ide shows each id as a link to its page - the fix, and when suppressing is right:

* [CHPD001](https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/CHPD001.md) The explanation would be sent to the caller
* [CHPD002](https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/CHPD002.md) The producer declares no problem of that name
* [CHPD003](https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/CHPD003.md) The problem is declared as a single shared instance
* [CHPD004](https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/CHPD004.md) A generic producer is never registered
* [CHPD005](https://github.com/ChristophHornung/problemdetails/blob/main/docs/rules/CHPD005.md) The problem is declared as a field

## The sample

[samples/](https://github.com/ChristophHornung/problemdetails/tree/main/samples) is a working api, and the host
the integration tests run against: a web project, and a domain library it references that holds the services
and the problems they throw. Nothing in the web project registers that library. The users and orders are seeded
so every path has one to take it; their ids are in `Sample.http`, which holds every request below.

```shell
dotnet run --project samples/Chorn.AspNetCore.ProblemDetails.Sample
```

| Request | What it shows |
|---|---|
| `POST /users/{cleo}/assign` | the happy path - 204 |
| `POST /users/{dan}/assign` | a problem thrown from the service, with a `userId` member - 409 |
| `DELETE /users/{ada}` | the same kind of response, returned from the controller - 403 |
| `POST /orders/1/ship` | a minimal api endpoint answering with a problem - 409 |
| `GET /users/{ada}/orders/3` | two producers sharing a problem name, both documented - 404 |
| `GET /problems` | every problem the application can produce, explanations included |
| `GET /problems/user/not-signed-up` | one problem, looked up by the type a caller received |
| `GET /problems/expected/418` | a deliberate failure - development only |
| `GET /swagger` | the examples in a ui, from either integration |

The producers there are `public` only because the web project names them across the reference; in a single
project they can be `internal`.

## Options

```csharp
builder.Services.AddExplainedProblemDetails(options =>
{
	options.ScanAssemblyOf<UserProblems>();          // exactly this assembly, no discovery
	options.AddProducer<UserProblems>();             // or no scanning at all
	options.ValidateDocumentedProblems = false;      // skip the startup name check
	options.RegisterExceptionHandler = false;        // register the handler yourself
	options.AddProblemDetailsServices = false;       // you call AddProblemDetails yourself
});
```

## A note on `type`

RFC 9457 treats `type` as the identifier a client matches on - `title` and `detail` are for humans and may
change - and wants it to be a URI reference: absolute, or relative like the sample's. Nothing here enforces it;
`type` is a plain string, and short slugs keep working.

## License

MIT.
