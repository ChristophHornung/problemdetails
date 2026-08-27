# CHPD003: The problem is declared as a single shared instance

| | |
|---|---|
| Category | `Chorn.ProblemDetails` |
| Severity | Warning |
| Enabled by default | Yes |

## Cause

A static `ExplainedProblemDetails` property on a producer has an initializer, so its problem is created once:

```csharp
internal static ExplainedProblemDetails NotSignedUp { get; } = new() { ... };
```

## Why it matters

Every reader of that property gets the same instance, for the life of the process. The package itself copies a
problem before adding anything to it, so `AsException(extensions)` is safe - but anything set on the instance
directly, before it is handed over, is set for every request from then on:

```csharp
ExplainedProblemDetails problem = UserProblems.NotSignedUp;
problem.Extensions["userId"] = userId;   // now on every future response of this problem
throw problem.AsException();
```

Two requests doing that at the same time can also corrupt the extensions dictionary.

## How to fix

Declare the problem with an expression body, so every reader gets its own:

```csharp
// Before
internal static ExplainedProblemDetails NotSignedUp { get; } = new() { ... };

// After
internal static ExplainedProblemDetails NotSignedUp => new() { ... };
```

The cost is one small allocation per read, at the moment a request is already failing.

## When to suppress

Do not. There is no benefit to a shared instance that a fresh one lacks, and the hazard only shows under load.
