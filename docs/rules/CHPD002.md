# CHPD002: The producer declares no problem of that name

| | |
|---|---|
| Category | `Chorn.ProblemDetails` |
| Severity | Error |
| Enabled by default | Yes |

## Cause

`[ProducesProblems<TProducer>("Name")]` names a problem that `TProducer` does not declare - as a static
`ExplainedProblemDetails` property of its own or of a base producer. Both spellings are checked: the attribute
applied to a controller or an action, and the attribute constructed as endpoint metadata on a minimal api.

Only producers declared in the same project are judged. Across a reference the compiler cannot see an
`internal` problem, and an error has to be right.

## Why it matters

`nameof` proves that a name exists, not that it exists on the producer being documented:

```csharp
using static MyApi.Problems.UserProblems;

[ProducesProblems<OrderProblems>(nameof(NotSignedUp))]   // compiles: NotSignedUp is a UserProblems member
```

Left alone, the startup validation fails the application with the same message. Without that validation, the
problem is silently dropped from the api description.

## How to fix

Name the problem on the producer that declares it, or document the producer that does:

```csharp
// Before
[ProducesProblems<OrderProblems>(nameof(NotSignedUp))]

// After
[ProducesProblems<UserProblems>(nameof(NotSignedUp))]
```

The message lists every problem the producer does declare.

## When to suppress

There is no right occasion. A name that does not resolve is always a mistake, and the rule never reports one
that would resolve at runtime - producers it cannot see whole are not judged at all. If a build has to pass
regardless, lower the severity in `.editorconfig`:

```ini
dotnet_diagnostic.CHPD002.severity = warning
```
