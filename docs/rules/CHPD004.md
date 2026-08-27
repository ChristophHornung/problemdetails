# CHPD004: A generic producer is never registered

| | |
|---|---|
| Category | `Chorn.ProblemDetails` |
| Severity | Warning |
| Enabled by default | Yes |

## Cause

A concrete producer has type parameters, or is nested inside a type that has:

```csharp
internal sealed class ProblemsOf<TEntity> : ProblemProducerBase { ... }

internal static class Outer<T>
{
	internal sealed class Problems : ProblemProducerBase { ... }
}
```

## Why it matters

The container cannot create an open generic implementation type, so `AddExplainedProblemDetails` skips such a
producer when it scans - and it has to, because registering it would fail the first resolve with an error naming
neither the package nor the mistake. Skipped means none of its problems exist: not in the storage, not in the
api description, not behind the explanation endpoint. Nothing else reports that.

## How to fix

Make the producer a plain, non-nested class. A problem is a static declaration; it has no use for a type
parameter.

```csharp
// Before
internal sealed class ProblemsOf<TEntity> : ProblemProducerBase { ... }

// After
internal sealed class EntityProblems : ProblemProducerBase { ... }
```

## When to suppress

Do not. The producer is not registered either way; suppressing the warning only hides that.
