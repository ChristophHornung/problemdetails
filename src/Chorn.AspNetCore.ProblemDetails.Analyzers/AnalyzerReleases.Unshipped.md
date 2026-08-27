; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CHPD001 | Chorn.ProblemDetails | Warning | The explanation would be sent to the caller
CHPD002 | Chorn.ProblemDetails | Error | The producer declares no problem of that name
CHPD003 | Chorn.ProblemDetails | Warning | The problem is declared as a single shared instance
CHPD004 | Chorn.ProblemDetails | Warning | A generic producer is never registered
CHPD005 | Chorn.ProblemDetails | Warning | The problem is declared as a field
