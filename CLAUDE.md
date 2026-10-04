# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

`AspNetCore.AppInfo` is a family of NuGet packages that adds an `/appinfo` endpoint to ASP.NET Core apps. The endpoint returns JSON describing the running application. GitHub repo: `dut00/aspnetcore-appinfo`.

- [docs/SPEC.md](docs/SPEC.md) is the behavioral contract: public API, JSON fields, masking rules, edge cases. **If the code, the plan and the spec disagree, the spec wins.** Update the spec when behavior changes on purpose.
- [docs/PLAN.md](docs/PLAN.md) holds the implementation plan and the numbered steps. Keep it current when something changes during implementation.
- [docs/draft.md](docs/draft.md) holds the author's original notes, in Polish and using the old name `Kudu.AppInfo`. It is history: don't edit it.

## Working rules

- **Everything in the repo is written in English**: code, comments, XML docs, READMEs, docs, commit messages. The user writes to you in Polish, so answer in Polish.
- **Never commit without asking.** After each plan step, run build and tests, summarize the result, then ask whether to commit and propose a message.
- Follow the steps in `docs/PLAN.md` in order, and don't start features that are out of scope for v1.

## Commands

```bash
dotnet build AspNetCore.AppInfo.slnx           # builds net8.0, net9.0 and net10.0
dotnet test AspNetCore.AppInfo.slnx            # tests run on net8.0 and net10.0
dotnet run --project samples/AspNetCore.AppInfo.Sample.Api   # then GET /appinfo
./build/pack.ps1                               # packs into ./artifacts (local NuGet feed)
```

The SDK is pinned in `global.json` (10.0.x). The machine has the .NET 8 and .NET 10 runtimes but **not .NET 9**. net9.0 builds fine, but its tests can't run locally, so test projects target only `net8.0;net10.0`.

## Layout

- `src/AspNetCore.AppInfo*/`: packable libraries. One package per folder; each has its own `README.md`, which goes into the package.
- `test/AspNetCore.AppInfo*.Tests/`: xUnit v3 + Shouldly. Integration tests use `WebApplication.CreateBuilder()` with `UseTestServer()`.
- `samples/AspNetCore.AppInfo.Sample.Api/`: minimal API that uses the packages through ProjectReference. `-p:UseLocalPackages=true` switches it to `./artifacts`.
- `Directory.Build.props`: shared TFMs, strict compiler settings and NuGet metadata. Projects under `src/` become packable automatically.
- `Directory.Packages.props`: Central Package Management. Add versions there, never in a csproj. `Microsoft.AspNetCore.TestHost` has one version per TFM.

## Conventions and gotchas

- `TreatWarningsAsErrors` and `GenerateDocumentationFile` are on for `src/`, so **every public member needs an XML doc comment**. A `cref` to a type or member that doesn't exist yet fails the build; use `<c>…</c>` until it does.
- Public extension methods live in the package's own namespace (`AspNetCore.AppInfo`, `AspNetCore.AppInfo.Environment`, …), not in `Microsoft.Extensions.DependencyInjection`. A single `using` should light up a package's API.
- Implementation details go in an `Internal` namespace and are `internal`. Test projects get access through `InternalsVisibleTo`.
- A new field is added through an `IAppInfoContributor` registered with `TryAddEnumerable`. Contributors run in registration order, and the JSON keeps the order in which keys were first set.
- Inside `AspNetCore.AppInfo.Environment`, write `System.Environment` in full, because the namespace shadows it.
- Don't put `.WithName(...)` on the mapped endpoint. Endpoint names must be globally unique, so mapping `/appinfo` twice would break routing. Use `.WithDisplayName(...)` instead.
- Masking must fail closed. If a connection string can't be parsed, mask the whole value.
