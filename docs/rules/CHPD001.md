# CHPD001: The explanation would be sent to the caller

| | |
|---|---|
| Category | `Chorn.ProblemDetails` |
| Severity | Warning |
| Enabled by default | Yes |

## Cause

An `ExplainedProblemDetails` is handed to something that serializes whatever it is given:

* `Results.Problem(problem)`, `TypedResults.Problem(problem)`, `Results.Json(problem)`, `Results.Ok(problem)`
  and the other `Results` / `TypedResults` factories taking an `object`, a type parameter or a `ProblemDetails`
* `ControllerBase` methods with such a parameter - `Ok(problem)`, `StatusCode(409, problem)`, `Json(problem)`
* `new ObjectResult(problem)` and everything deriving from `ObjectResult`, such as `OkObjectResult`
* `new JsonResult(problem)`
* `Response.WriteAsJsonAsync(problem)`
* `JsonSerializer.Serialize(problem)` and the other `JsonSerializer` methods

Or a public controller action returns one directly - as `ExplainedProblemDetails`, or inside `Task<>`,
`ValueTask<>` or `ActionResult<>`.

## Why it matters

`ExplainedProblemDetails` derives from `ProblemDetails`, so every framework api accepts it - and every one of
them serializes the *runtime* type. The `Explanation` goes out in the response body. It is written for a
developer reading the api documentation and may say more about the service than a caller should see; it is also
usually longer than a response body should be.

## How to fix

Answer with the problem through one of the two ways that copy only the members a caller is meant to see:

```csharp
// Before - the whole object, explanation included
return Results.Problem(UserProblems.NotSignedUp);

// After - thrown from anywhere, turned into the response by the handler
throw UserProblems.NotSignedUp.AsException();

// After - returned from a controller
return this.Problem(UserProblems.NotSignedUp);
```

## When to suppress

Exactly one case: an endpoint whose job is to hand the explanation out, like the one
`MapProblemExplanations` maps. The explanation is the answer there, not a leak.

```csharp
#pragma warning disable CHPD001 // This endpoint exists to return the explanation.
return TypedResults.Ok(details);
#pragma warning restore CHPD001
```

## Not reported

* Methods marked `[NonAction]`, static methods and generic methods on a controller - they are not endpoints.
* The `this.Problem(problem)` extension of this package. It is named like the `ControllerBase` methods on
  purpose, and it is not a sink - unless a base controller declares its own `Problem(ProblemDetails)`, which
  then takes the call over and is reported.
