# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

`Dut00.AppInfo` is a family of NuGet packages (renamed from `AspNetCore.AppInfo`, whose ID is reserved on nuget.org) that adds an `/appinfo` endpoint to ASP.NET Core apps. The endpoint returns JSON describing the running application. GitHub repo: `dut00/appinfo`.

- [docs/SPEC.md](docs/SPEC.md) is the behavioral contract: public API, JSON fields, masking rules, edge cases. **If the code, the plan and the spec disagree, the spec wins.** Update the spec when behavior changes on purpose.
- [docs/PLAN.md](docs/PLAN.md) holds the implementation plan and the numbered steps. Keep it current when something changes during implementation.
- [docs/draft.md](docs/draft.md) holds the author's original notes, in Polish and using the old name `Kudu.AppInfo`. It is history: don't edit it.

## Working rules

- **Everything in the repo is written in English**: code, comments, XML docs, READMEs, docs, commit messages. The user writes to you in Polish, so answer in Polish.
- **Never commit without asking.** After each plan step, run build and tests, then run the `code-reviewer` subagent (`.claude/agents/code-reviewer.md`) on the uncommitted changes and fix or explicitly raise its findings. Then summarize the result, ask whether to commit and propose a message.
- Follow the steps in `docs/PLAN.md` in order, and don't start features that are out of scope for v1.

## Commands

```bash
dotnet build Dut00.AppInfo.slnx           # builds net8.0, net9.0 and net10.0
dotnet test --solution Dut00.AppInfo.slnx # tests run on net8.0 and net10.0
dotnet run --project samples/Dut00.AppInfo.Sample.Api   # then GET /appinfo
pwsh ./build/pack.ps1                          # packs into ./artifacts (local NuGet feed), clears cached copies
                                               # Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File build/pack.ps1
dotnet run --project samples/Dut00.AppInfo.Sample.Api -p:UseLocalPackages=true   # sample on the packed packages
```

The SDK is pinned in `global.json` (10.0.x). The machine has the .NET 8 and .NET 10 runtimes but **not .NET 9**. net9.0 builds fine, but its tests can't run locally, so test projects target only `net8.0;net10.0`.

Tests use xUnit v3 on Microsoft.Testing.Platform: `global.json` opts into the new `dotnet test` mode, so pass the solution with `--solution` (VSTest options such as `--logger` don't apply). Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`.

## Layout

- `src/Dut00.AppInfo*/`: packable libraries. One package per folder; each has its own `README.md`, which goes into the package.
- `test/Dut00.AppInfo*.Tests/`: xUnit v3 + Shouldly. Integration tests use `WebApplication.CreateBuilder()` with `UseTestServer()`.
- `test/Infrastructure/`: shared test helpers (`TestApp`, log capture, JSON helpers; namespace `Dut00.AppInfo.Testing`), linked into every test project by `test/Directory.Build.props`. Don't copy them into a test project.
- `samples/Dut00.AppInfo.Sample.Api/`: minimal API that uses the packages through ProjectReference. `-p:UseLocalPackages=true` switches it to `./artifacts`.
- `.github/`: `workflows/ci.yml` builds and tests on Linux, Windows and macOS, then packs (skipped when only the root README, `docs/`, `LICENSE` or Claude files change; package READMEs still trigger it); `workflows/release.yml` publishes to nuget.org on a `v*` tag (Trusted Publishing, `nuget` environment, `NUGET_USER` variable); `dependabot.yml` updates NuGet packages (not `Microsoft.AspNetCore.TestHost`: bump its per-TFM versions by hand), GitHub Actions and the SDK in `global.json` (within .NET 10). Tests must pass on Linux too: don't assume `\` is a path separator.
- `assets/icon.png`: the package icon. `Directory.Build.props` adds it to every package only if the file exists.
- `Directory.Build.props`: shared TFMs, strict compiler settings and NuGet metadata. Projects under `src/` become packable automatically.
- `.claude/agents/code-reviewer.md`: read-only review subagent that checks changes against the spec and these conventions.
- `Directory.Packages.props`: Central Package Management. Add versions there, never in a csproj. The repo's own packages use `$(AppInfoPackageVersion)`; bump the version there (in `Directory.Build.props`), not in `<Version>`. `Microsoft.AspNetCore.TestHost` has one version per TFM.

## Conventions and gotchas

- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on for `src/`, so **every public member needs an XML doc comment**. A `cref` to a type or member that doesn't exist yet fails the build; use `<c>…</c>` until it does.
- Public extension methods live in the package's own namespace (`Dut00.AppInfo`, `Dut00.AppInfo.Environment`, …), not in `Microsoft.Extensions.DependencyInjection`. A single `using` should light up a package's API.
- Implementation details go in an `Internal` namespace and are `internal`. Test projects get access through `InternalsVisibleTo`.
- A new field is added through an `IAppInfoContributor` registered with `TryAddEnumerable` (the one exception is `WithProperty`: all its contributors share a type, so each is added with `AddSingleton`). Contributors run in registration order, and the JSON keeps the order in which keys were first set.
- Inside `Dut00.AppInfo.Environment`, write `System.Environment` in full, because the namespace shadows it.
- Don't put `.WithName(...)` on the mapped endpoint. Endpoint names must be globally unique, so mapping `/appinfo` twice would break routing. Use `.WithDisplayName(...)` instead.
- Masking must fail closed. If a connection string can't be parsed, mask the whole value.
