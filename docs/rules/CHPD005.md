# CHPD005: The problem is declared as a field

| | |
|---|---|
| Category | `Chorn.ProblemDetails` |
| Severity | Warning |
| Enabled by default | Yes |

## Cause

A producer declares a static field of type `ExplainedProblemDetails`:

```csharp
internal static readonly ExplainedProblemDetails NotSignedUp = new() { ... };
```

## Why it matters

Only static properties are read as problems. A field looks like a declaration and is never found: the problem is
missing from the storage and the api description, and an endpoint that names it with `nameof` is reported by
[CHPD002](CHPD002.md) as documenting a problem the producer does not declare - which is confusing, because it
is right there.

## How to fix

Make it a static property with an expression body. A field would have the shared-instance hazard of
[CHPD003](CHPD003.md) as well, so an initializer is not the fix.

```csharp
// Before
internal static readonly ExplainedProblemDetails NotSignedUp = new() { ... };

// After
internal static ExplainedProblemDetails NotSignedUp => new() { ... };
```

## When to suppress

If the field is genuinely not meant to be a problem - a template other problems are built from, say - rename it
so it does not read as one, or move it out of the producer. Suppressing is only right when it has to stay where
it is and be what it is; then say why.
