# AGENTS.md

Three NuGet packages that turn RFC 9457 problem details into something you declare once and get everywhere:
thrown from any layer, documented in the open api description, and looked up by the developer who received it.

Extracted from a production ASP.NET Core service, so the shape is proven - keep it that way rather than
generalising it further without a reason.

## Layout

```
src/Chorn.AspNetCore.ProblemDetails              the package: declaring, throwing, looking up. No third party deps.
src/Chorn.AspNetCore.ProblemDetails.Analyzers    CHPD001-005, packed into the core package. netstandard2.0.
src/Chorn.AspNetCore.ProblemDetails.Swashbuckle  the Swashbuckle IOperationFilter
src/Chorn.AspNetCore.ProblemDetails.OpenApi      the Microsoft.AspNetCore.OpenApi IOpenApiOperationTransformer
src/Shared                                       source compiled into both open api packages, not its own package
samples/Chorn.AspNetCore.ProblemDetails.Sample   a working api - and the host the integration tests run against
samples/....Sample.Domain                        its services and the problems they throw, found by discovery
tests/Chorn.AspNetCore.ProblemDetails.Tests      TUnit, unit and integration
tests/....Tests.App                              stands in for a web project, references Tests.Domain
tests/....Tests.Domain                           stands in for a domain library that declares problems
tests/....Tests.Producers                        deliberately broken producers, in their own assembly on purpose
tests/....Tests.Startup                          a shared-startup library with no producers, also on purpose
```

`ApplicationAssemblies` mirrors how mvc finds controllers: start at `IHostEnvironment.ApplicationName` (read off
the `IServiceCollection`, where the host registers it as an instance), take the application's dependency closure
and keep the libraries that depend on this package. Two things are load-bearing:

* **The graph decides, not the assembly.** Asking an assembly for its references means loading it, so deciding
  from the dependency file is what keeps startup from loading every package an application has. Measured on an
  application with five ordinary packages: one assembly loaded, not nineteen.
* **A library id is not an assembly name.** `Humanizer.Core` holds `Humanizer.dll`, so the candidate names come
  from each library's `runtime` files, with the id only as a last resort.

The dependency file is read with `System.Text.Json` rather than `Microsoft.Extensions.DependencyModel`, because
the core package has no package references and is not going to get one. Walking assembly references is the
fallback when no dependency file describes the application - single file, trimmed - and it misses a project
reference whose types the application never uses, which is what `ApplicationAssembliesTests` pins down.

Verify a change here against a **packed** package restored with `--packages <isolated-dir>`. A same-version
package is otherwise served from the extracted global cache and the new one is never read.

The analyzer is referenced by the core project and by the sample with `OutputItemType="Analyzer"`, so both are
analyzed by it. Analyzers do not flow across a `ProjectReference`, which is why the analyzer tests compile
snippets instead.

## Resources to consider

- The `README.md` is the user-facing documentation. Keep it correct when the api surface changes.
- The sample is documentation too. If a feature is not visible there, a reader will not find it.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457), which obsoleted RFC 7807.

## DOs

- Keep `Chorn.AspNetCore.ProblemDetails` free of package references. It has a `FrameworkReference` to
  `Microsoft.AspNetCore.App` and nothing else - that is a feature, and the reason the open api support is split
  off into its own packages.
- Put anything both open api integrations need into `src/Shared`, linked into both projects. They differ only in
  where the operation and the schema come from.
- Write an xml documentation comment for every public member. `GenerateDocumentationFile` is on, and the
  comments are what ships in the package.
- Follow the `.editorconfig`: tabs, `using` directives inside the namespace, `this.` on instance members, the
  declaring type on static members.
- Cover new behaviour with a test. Integration tests go through the sample application over real HTTP - that is
  what catches the things reflection-level tests do not, such as a route that does not match.
- Leave `src/Chorn.AspNetCore.ProblemDetails.OpenApi/buildTransitive/*.targets` alone. It looks redundant and is
  not: `Microsoft.AspNetCore.OpenApi` ships its xml comment source generator as an analyzer that reaches our
  consumers, but ships the property enabling its interceptors in `build/`, which does not. Without the file, a
  consumer with `GenerateDocumentationFile` that does not reference `Microsoft.AspNetCore.OpenApi` directly
  fails with CS9137. Verify a change to it by building a project against the packed `.nupkg`, not against a
  project reference - project references do not carry build assets.
- A new analyzer rule comes with three things: a row in `AnalyzerReleases.Unshipped.md` (the build fails the
  RS2008 check otherwise), a page `docs/rules/CHPD00N.md` in the shape of the existing ones, and a row in
  `docs/rules/README.md`. The descriptor's help link points at the page, so an ide can open it from the id.
- Commit messages follow the [gitmoji](https://gitmoji.dev) convention: the emoji first, then an imperative
  summary - `🐛 Fix the route that did not match`, `📝 Point the readme at the sample`, `✨ Ship an analyzer`.

## DONTs

- Do not add a producer or a problem to the sample without checking the tests that count them
  (`RouteWithoutAType_IsAnsweredWithEveryProblem`, and the storage tests). Do not reuse a seeded user or order
  across tests that change it - the host is shared and the tests run in parallel; `UserDirectory.Known` says
  which path each one is there for.
- Do not put an `[ProducesProblems<...>]` attribute naming a problem that does not exist anywhere except on
  `BrokenlyDocumented` in the test assembly. The startup validation scans the whole assembly, and every other
  registration test would start failing.
- Do not put a deliberately broken *producer* - one whose problem throws, or two sharing a type - in the test
  assembly either, and do not reference `Tests.Producers` from any assembly a test uses as the application.
  Discovery walks the whole dependency closure, so one broken fixture in it fails every discovery test.
- Do not let a test rely on the implicit discovery while the application resolves to the test assembly, which
  references `Tests.Producers`. Name the assembly, or stand a `FakeHost` in front of `Tests.App`.
- Do not let the `Explanation` leak into a response body. It is for the api description and the explanation
  endpoint; `ToResponseJson` is the one place that decides what a caller sees.

## Building and testing

```shell
dotnet build Chorn.AspNetCore.ProblemDetails.slnx
dotnet test tests/Chorn.AspNetCore.ProblemDetails.Tests/Chorn.AspNetCore.ProblemDetails.Tests.csproj
```

The test runner is Microsoft.Testing.Platform, pinned in `global.json`.

To look at the sample by hand:

```shell
dotnet run --project samples/Chorn.AspNetCore.ProblemDetails.Sample
```

`/swagger/v1/swagger.json` and `/openapi/v1.json` are the two documents, `/problems` lists every problem.

## Releasing

1. Bump `<Version>` in `Directory.Build.props`. It governs all three packages - they ship together.
   Move the analyzer rules from `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` under the new
   version heading at the same time.
2. Add the release notes to `<PackageReleaseNotes>` in each of the three `src/**/*.csproj`.
3. Merge to `main`, then run the `Deployment` workflow manually.

The workflow refuses to publish a version that is already on nuget.org, pushes all three packages and tags the
release.

Publishing uses nuget.org trusted publishing, not an api key: `NuGet/login` exchanges the run's OIDC token for a
key that lasts minutes, so nothing long-lived is stored anywhere. One-time setup: on nuget.org, under the
account's *Trusted Publishing*, add a policy for the repository `ChristophHornung/problemdetails` and the workflow
file `deploy.yml`; in the repository settings, add the secret `NUGET_USER` holding the nuget.org username that
policy belongs to. That is the only secret.
