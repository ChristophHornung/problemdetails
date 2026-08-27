# Analyzer rules

`Chorn.AspNetCore.ProblemDetails` ships a Roslyn analyzer inside the package. Referencing the package is enough;
the rules need no configuration. Each id in an IDE's error list links to its page here.

| Rule | Severity | Title |
|---|---|---|
| [CHPD001](CHPD001.md) | Warning | The explanation would be sent to the caller |
| [CHPD002](CHPD002.md) | Error | The producer declares no problem of that name |
| [CHPD003](CHPD003.md) | Warning | The problem is declared as a single shared instance |
| [CHPD004](CHPD004.md) | Warning | A generic producer is never registered |
| [CHPD005](CHPD005.md) | Warning | The problem is declared as a field |

All rules are in the category `Chorn.ProblemDetails`.

## Changing a severity

In an `.editorconfig` that covers the project:

```ini
# Turn one rule off
dotnet_diagnostic.CHPD001.severity = none

# Or make it fail the build
dotnet_diagnostic.CHPD003.severity = error
```

Suppress a single occurrence in code with `#pragma warning disable CHPD001` / `#pragma warning restore CHPD001`
around it, or `[SuppressMessage("Chorn.ProblemDetails", "CHPD001", Justification = "...")]` on the member. Each
page says when that is the right thing to do.

## When a rule was added

[AnalyzerReleases.Shipped.md](../../src/Chorn.AspNetCore.ProblemDetails.Analyzers/AnalyzerReleases.Shipped.md)
lists every rule by the version it first shipped in;
[AnalyzerReleases.Unshipped.md](../../src/Chorn.AspNetCore.ProblemDetails.Analyzers/AnalyzerReleases.Unshipped.md)
the ones on `main` that have not.
