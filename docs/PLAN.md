# Plan: Dut00.AppInfo – NuGet package family exposing an `/appinfo` endpoint

## Context
Status: **steps 1–11 are implemented** (v1, released as `0.1.0-preview.*`). **v0.2: steps 12–17 are done, step 18 is planned**: four new packages (Build, Runtime, Container, Server) and the `Process` group in Environment. The repo started with only `draft.md` (Polish notes). Goal: a family of NuGet packages, structured like Xabaril/AspNetCore.Diagnostics.HealthChecks, that adds an `/appinfo` endpoint to ASP.NET Core apps. The endpoint returns JSON describing the application (name, version, environment, owner, …) and can be extended through separate packages.

**All code, comments, XML docs, READMEs and this plan are written in English.**

Naming: packages, namespaces and projects use the `Dut00.AppInfo.*` prefix; the GitHub repo is `dut00/appinfo`. The original notes used `Kudu.AppInfo`. The project was first named `AspNetCore.AppInfo` (mirroring `AspNetCore.HealthChecks.*`), but nuget.org rejected that ID on the first release: "The package ID is reserved". It was renamed to the owner-specific `Dut00.AppInfo`, whose prefix can be reserved for this account.

The behavioral contract (public API, JSON fields, masking rules, edge cases) is defined in [SPEC.md](SPEC.md). If this plan and SPEC.md disagree, SPEC.md wins.

Agreed decisions:
- Target frameworks: `net8.0;net9.0;net10.0`
- Packages: core + separate extension packages. v1 ships **Configuration, Environment and ConnectionStrings (masked)**. v0.2 adds **Build, Runtime, Container and Server**, one package per topic, none with extra dependencies.
- Architecture: contributors implementing `IAppInfoContributor`, registered in DI. API: `AddAppInfo()` returns `IAppInfoBuilder`, then `.WithXxx()`. Endpoint: `MapAppInfo(pattern = "/appinfo")`.
- Core fields: ApplicationName, Version, Environment, IsProduction, plus `WithOwner` and custom fields (`WithProperty`)
- JSON shape: single values sit at the root (`HostName`, `Urls`, ...); a set of related values is grouped under one object key (`Process`, `Build`, `Runtime`, `Container`). Key casing is configurable, PascalCase by default, and applies to nested properties too.
- Security: `MapAppInfo` returns `IEndpointConventionBuilder`, so the user can chain `.RequireAuthorization()` etc. The README carries a warning.
- Layout: `src/ test/ samples/`, `.slnx`, `Directory.Build.props`, Central Package Management
- Tests: xUnit + Shouldly, unit and integration (TestServer)
- Local dev: the sample uses ProjectReference, and a pack script builds a local feed. All packages share one version.
- Workflow: **after each implementation step, run build, tests and the `code-reviewer` subagent, then stop and ask the user whether to commit. Never commit automatically.**

## Repository layout
```
appinfo-nuget/
├─ Dut00.AppInfo.slnx
├─ Directory.Build.props        # TFMs, LangVersion, Nullable, ImplicitUsings, TreatWarningsAsErrors,
│                               # NuGet metadata (Authors, MIT license, RepositoryUrl, AppInfoPackageVersion=0.1.0 -> Version,
│                               # PackageReadmeFile, GenerateDocumentationFile, Deterministic, snupkg symbols)
├─ Directory.Packages.props     # CPM: xunit.v3 (Microsoft.Testing.Platform, no VSTest packages),
│                               # Shouldly, Microsoft.AspNetCore.TestHost (versions per TFM), TimeProvider.Testing,
│                               # and the repo's own packages for the sample's local-feed mode
├─ global.json                  # SDK 10.0.x, rollForward latestFeature, test runner = Microsoft.Testing.Platform
├─ nuget.config                 # nuget.org + local ./artifacts source
├─ .gitignore, .gitattributes, .editorconfig, README.md, LICENSE
├─ CLAUDE.md                    # guidance for Claude Code; .claude/agents/code-reviewer.md is the review subagent
├─ docs/PLAN.md                 # copy of this plan
├─ docs/SPEC.md                 # behavioral contract (wins over this plan)
├─ docs/draft.md                # original notes (moved from root)
├─ .github/                     # workflows/ci.yml (build + test on Linux, Windows, macOS; pack),
│                               # workflows/release.yml (v* tag -> nuget.org via Trusted Publishing), dependabot.yml
├─ assets/icon.png              # package icon (added to packages only when present)
├─ build/pack.ps1               # dotnet pack -c Release -o ./artifacts (+ clears dut00.appinfo* from NuGet cache)
├─ src/
│  ├─ Dut00.AppInfo/
│  ├─ Dut00.AppInfo.Configuration/
│  ├─ Dut00.AppInfo.Environment/
│  ├─ Dut00.AppInfo.ConnectionStrings/
│  ├─ Dut00.AppInfo.Build/            # v0.2
│  ├─ Dut00.AppInfo.Runtime/          # v0.2
│  ├─ Dut00.AppInfo.Container/        # v0.2
│  └─ Dut00.AppInfo.Server/           # v0.2
├─ test/
│  ├─ Directory.Build.props     # IsPackable=false, TFMs net8.0;net10.0 (only these runtimes are installed),
│  │                            # links test/Infrastructure into every test project
│  ├─ Infrastructure/           # shared helpers: TestApp (TestServer), log capture, JSON helpers
│  ├─ Dut00.AppInfo.Tests/
│  ├─ Dut00.AppInfo.Configuration.Tests/
│  ├─ Dut00.AppInfo.Environment.Tests/
│  ├─ Dut00.AppInfo.ConnectionStrings.Tests/
│  └─ Dut00.AppInfo.{Build,Runtime,Container,Server}.Tests/   # v0.2
└─ samples/
   ├─ Directory.Build.props     # single TFM net10.0, not packable
   └─ Dut00.AppInfo.Sample.Api/  # minimal API; ProjectReference to all packages, or the local feed
```

## Core design: `src/Dut00.AppInfo`
Depends only on `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

- `IAppInfoContributor`:
  `ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)`
- `AppInfoContext` exposes `IServiceProvider Services`, `HttpContext HttpContext` and `void Set(string key, object? value)`. It is backed by a list plus a key index, so keys keep the order in which they were first set. If two contributors write the same key, the later one wins and a warning is logged.
- `IAppInfoBuilder { IServiceCollection Services; }`, implemented by `AppInfoBuilder`
- `AppInfoOptions`:
  - `JsonSerializerOptions JsonSerializerOptions`. Defaults: `WriteIndented = true`, `PropertyNamingPolicy = null`, `DictionaryKeyPolicy = null` (PascalCase), `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (readable `+`, `<`, non-ASCII; added in v0.2). It applies to values, not to root keys.
  - `JsonNamingPolicy? KeyNamingPolicy` converts root keys (applied when building the `JsonObject`) and sets `JsonSerializerOptions.PropertyNamingPolicy` for nested object properties. Nested dictionary keys (for example connection string names) stay as-is.
- `AddAppInfo(this IServiceCollection, Action<AppInfoOptions>? configure = null)` registers the options and `CoreAppInfoContributor` (via `TryAddEnumerable`) and returns the builder.
- `CoreAppInfoContributor` writes:
  - `ApplicationName` from `IHostEnvironment.ApplicationName`
  - `Version` from `Assembly.GetEntryAssembly()?.GetName().Version`
  - `Environment` from `EnvironmentName`
  - `IsProduction` from `IsProduction()`
- Builder extensions in core:
  - `WithOwner(string owner)` adds the `Owner` key. The last call wins.
  - `WithProperty(string key, object? value)` and `WithProperty(string key, Func<IServiceProvider, object?> factory)` add custom fields through `DelegateAppInfoContributor`.
  - `WithContributor<T>() where T : class, IAppInfoContributor` is the hook for third-party extensions.
- `MapAppInfo(this IEndpointRouteBuilder, string pattern = "/appinfo")`:
  - registers a `MapGet` that runs every `IEnumerable<IAppInfoContributor>` in registration order;
  - writes a `JsonObject` (keeps key order; root keys converted by `KeyNamingPolicy`) with `WriteAsJsonAsync` and `options.JsonSerializerOptions`;
  - returns `IEndpointConventionBuilder` and sets the display name `AppInfo` (not `WithName`, which must be globally unique and would break mapping twice).
- Each package has its own `README.md` (PackageReadmeFile).

Namespaces match package names (`Dut00.AppInfo`, `Dut00.AppInfo.Environment`, …), and each package's extension methods live in its namespace, as in the draft (`using Dut00.AppInfo.Environment;`). Because that namespace collides with `System.Environment`, the Environment project must write `System.Environment` in full.

## Extensions
Each extension is a separate csproj with a ProjectReference to core (packed as a NuGet dependency), one contributor class, one extension-method class, an options class where needed, and internal helpers (for example `ConnectionStringMasker`).

- **Dut00.AppInfo.Configuration**: `WithConfigurationDetails()` adds `ConfigurationsFiles: string[]`.
  - Source: `IConfiguration as IConfigurationRoot`, then `Providers.OfType<FileConfigurationProvider>()`; the path is the file's `PhysicalPath`, else `PhysicalFileProvider.Root` + path for a missing file, else `Source.Path`. `ChainedConfigurationProvider.Configuration` is searched too, once per root.
  - Missing optional files are left out by default; the `IncludeMissingOptionalFiles` option includes them. Required files are always listed.
- **Dut00.AppInfo.Environment**: `WithEnvironmentDetails()` adds four keys:
  - `Process` (v0.2; replaced the v1 key `ApplicationProcessUptime`): `Id`, `StartTime` and `Uptime`, the process start time to now as a TimeSpan. The uptime is computed on each request with the app's `TimeProvider` if registered (the package registers none), otherwise `TimeProvider.System`, so tests can control it; it is `null` when the start time is unknown.
  - `HostName`: `System.Environment.MachineName`
  - `ContentRootPath`: from `IHostEnvironment`
  - `AssemblyLocation`: `Assembly.GetEntryAssembly()?.Location`, or `null` when empty (single-file apps)
- **Dut00.AppInfo.ConnectionStrings**: `WithConnectionStrings(Action<ConnectionStringsOptions>? configure = null)` adds `ConnectionStrings: { "<name>": "<masked value>" }`, read from `IConfiguration.GetSection("ConnectionStrings")`.
  - Masking parses each value with `DbConnectionStringBuilder`. Keys that contain an entry of `SensitiveKeys` get the value `***`, and so do values that hide a secret (nested `Password=` pairs, URLs with credentials or a query). See SPEC §5 for the full fail-closed rules.
  - Default `SensitiveKeys` (case-insensitive, matched as substrings): Password, Pwd, PSW, Pass, User ID, UID, User, Username, Key, AccountKey, SharedAccessKey, SharedAccessSignature, AccessKey, ApiKey, Secret, Token, Credential, Authorization, Signature, Bearer.
  - If a value can't be parsed, is URI-style, isn't in `key=value;` format (for example Redis) or has a split ODBC brace, the whole value becomes `***`.
  - Both `SensitiveKeys` and the mask string can be changed in options.

## v0.2 extensions
The contract (field names, sources, precedence, `null` rules) is in SPEC §2, §4.2, §7 and §8. Implementation notes:

- **Same skeleton as v1 extensions**: csproj with a ProjectReference to core, `<Name>AppInfoBuilderExtensions` in the package namespace, options class where needed, contributor and helpers in `Internal`, package `README.md`, a test project, a line in the slnx. Contributors are registered with `TryAddEnumerable` as singletons when they hold no per-request state.
- **Groups are typed**: each group (`Process`, `Build`, `Runtime`, `Container`) is an internal sealed record serialized by `System.Text.Json`, so property order is the declaration order and `KeyNamingPolicy` (via `PropertyNamingPolicy`) applies to nested names without extra code. Values that must keep an exact format are stored preformatted as strings. Consequence: apps that disable reflection-based serialization (trimmed/AOT with a source-generated-only resolver) can't serialize the groups; AOT isn't a goal.
- **Testable host access**: environment variables, files and the entry assembly's build attributes are read through small internal seams (an internal type with a static `Current`, resolved as `GetService<T>() ?? T.Current`), used only where a test can't control the input otherwise, which tests register to replace. Under Microsoft.Testing.Platform the entry assembly is the test project, so build attributes can't be varied any other way. Tests never set real environment variables, because tests run in parallel and share the process.
- **Environment** (`WithEnvironmentDetails`): replaces the root key `ApplicationProcessUptime` with the `Process` group (`Id`, `StartTime`, `Uptime`), first among its keys. The process start time is read once into a static `DateTimeOffset?` (null when the platform doesn't expose it, which makes both `StartTime` and `Uptime` null); the v1 `ProcessStartTime` DI seam and its first-use fallback are gone, because tests control the uptime through `FakeTimeProvider` relative to the real start time. `StartTime` is written as a string, `UtcDateTime.ToString("O", CultureInfo.InvariantCulture)` (System.Text.Json would trim fraction digits, and a `DateTimeOffset` would end in `+00:00`).
- **Build** (`WithBuildDetails`): `BuildDetailsOptions { ConfigurationSection = "Build", CommitEnvironmentVariables, BranchEnvironmentVariables, BuildIdEnvironmentVariables }`. Entry assembly attributes come from an internal `BuildAssemblyInfo(string? InformationalVersion, IReadOnlyDictionary<string, string> Metadata)` seam whose `Current` is read once; configuration and environment variables per request. The commit fallback parses the `+` suffix of `InformationalVersion`. The package README shows how to stamp `AssemblyMetadata` from CI (GitHub Actions, Azure DevOps, GitLab).
- **Runtime** (`WithRuntimeDetails`): one static value built from `RuntimeInformation` once. All record properties are `string`; architectures are stored as `Architecture.ToString()`, because System.Text.Json writes enums as numbers and a user's `JsonStringEnumConverter` could change their casing.
- **Container** (`WithContainerDetails`): `ContainerDetailsOptions { PodNameVariable = "POD_NAME", NamespaceVariable = "POD_NAMESPACE", NodeNameVariable = "NODE_NAME" }`. The result is computed once per contributor instance (singleton) on the first request, thread-safely (`Lazy<T>`). File reads catch I/O and access exceptions.
- **Server** (`WithServerAddresses`): resolves `IServer` (optional, a host singleton) once when the contributor is created, reads `Features.Get<IServerAddressesFeature>()?.Addresses` per request and copies it to an array by enumerating (Kestrel snapshots under a lock while enumerating; `Count` + `CopyTo` could race with a rebind after a configuration reload).

## Sample: `samples/Dut00.AppInfo.Sample.Api`
- `Program.cs` follows the draft: `AddAppInfo().WithConfigurationDetails().WithEnvironmentDetails().WithConnectionStrings().WithOwner(...).WithProperty("Team", "red")`, then `app.MapAppInfo()`.
- `appsettings.json` contains a sample connection string with a password, to show masking.
- `launchSettings.json` opens `/appinfo`.
- The sample uses ProjectReference by default. Building with `-p:UseLocalPackages=true` switches to PackageReference against `./artifacts` (conditional ItemGroup in the csproj). Package versions come from `AppInfoPackageVersion` in `Directory.Build.props` (separate from `Version`, which the sample overrides), listed in `Directory.Packages.props`.
- `build/pack.ps1` (run with `pwsh ./build/pack.ps1`, or `powershell -ExecutionPolicy Bypass -File build/pack.ps1`; `-Version` overrides `AppInfoPackageVersion`) packs into `./artifacts` and removes `dut00.appinfo*` from the NuGet global packages folder, because local packs reuse the same version and NuGet never refreshes a cached version.

## Tests (xUnit + Shouldly)
- Shared helpers in `test/Infrastructure/`, compiled into every test project by `test/Directory.Build.props`: `TestApp` uses `WebApplication.CreateBuilder()` + `builder.WebHost.UseTestServer()` with a fixed application name, environment and content root, captures logs, and offers `GetAppInfoAsync()` (a parsed `JsonElement`) or `GetAppInfoStringAsync()` (the raw body).
- **Core** covers, among others:
  - default path and custom path;
  - core fields, `WithOwner`, and `WithProperty` (value and factory);
  - key order and key overwrite;
  - `KeyNamingPolicy = CamelCase` changes the keys;
  - `.RequireAuthorization()` makes the endpoint return 401 (proves conventions apply);
  - a custom `IAppInfoContributor`;
  - registration idempotency, argument validation (keys, patterns, plain `null`, delegate values), `MapAppInfo` without `AddAppInfo`, GET only, display name without endpoint name, key collisions after the naming policy, and contributor exceptions reaching the app's error handling.
- **Configuration**: JSON, XML and INI files in a temp directory; full paths in load order; missing optional files left out or included by option; chained configurations; non-physical providers.
- **Environment**: fields are present with the right types; uptime is checked with a fake `TimeProvider`; HostName equals MachineName.
- **ConnectionStrings**: masker unit tests (SQL Server, PostgreSQL, Azure, ODBC, Redis, URIs, unparseable values, custom keys) including a corpus of secret-bearing inputs from three adversarial reviews that must never appear in the output, plus integration tests.
- **Environment (v0.2)**: `Process` has `Id`, `StartTime`, `Uptime` in that order; `Id` equals `System.Environment.ProcessId`; `StartTime` equals the real process start time, is UTC with a `Z` suffix, and `StartTime` and `Uptime` are both `null` when the start time is unknown (unit-tested through `CreateProcessDetails`).
- **Build**: every precedence level per property (configuration > assembly metadata > `+` suffix > environment variables), blank values skipped, custom section and variable lists, `+` suffix that isn't hex ignored, all `null` when nothing is found, camelCase nested names.
- **Runtime**: the group has all five properties with the values from `RuntimeInformation`.
- **Container**: each rule of SPEC §8 with fake environment variables and files (no container, Docker, Podman, official .NET image, Kubernetes with and without Downward API variables, unreadable namespace file), custom variable names.
- **Server**: `[]` under TestServer (its addresses feature is empty); addresses from a fake `IServer` with `IServerAddressesFeature`; `null` for a fake `IServer` without the feature.

## Implementation steps
After **every** step: run `dotnet build` and `dotnet test` (once tests exist), run the `code-reviewer` subagent and fix or raise its findings, summarize the result, then **ask the user whether to commit**. Don't commit without approval.

1. **Repo bootstrap**: `git init`, `.gitignore` (dotnet template), `.editorconfig`, `global.json`, `LICENSE` (MIT), move `draft.md` to `docs/draft.md`, copy this plan to `docs/PLAN.md`, root `README.md` stub.
2. **Build infrastructure**: `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, empty `Dut00.AppInfo.slnx`.
3. **Core package skeleton**: `src/Dut00.AppInfo` csproj, `IAppInfoContributor`, `AppInfoContext`, `IAppInfoBuilder`, `AppInfoOptions`, `AddAppInfo`, `MapAppInfo`, `CoreAppInfoContributor`.
4. **Core builder extensions**: `WithOwner`, `WithProperty` (both overloads), `WithContributor<T>`, `DelegateAppInfoContributor`, `KeyNamingPolicy`.
5. **Core tests**: `test/Directory.Build.props`, `test/Dut00.AppInfo.Tests` with the TestServer helper and all core scenarios.
6. **Sample app**: `samples/Dut00.AppInfo.Sample.Api` using core only. Check by hand with `dotnet run` + `curl /appinfo`.
7. **Environment package + tests**, then add it to the sample.
8. **Configuration package + tests**, then add it to the sample.
9. **ConnectionStrings package + tests** (masker unit tests and an integration test), then add it to the sample.
10. **Packaging and local feed**: per-package READMEs, `build/pack.ps1`, the `UseLocalPackages` switch in the sample, then check the `.nupkg` contents.
11. **Documentation**: root `README.md` in English (quick start, every `With*` method, sample response, security warning, how to write a custom contributor). Update `docs/PLAN.md` if anything changed during implementation.

### v0.2 steps
Each package step includes: the package (csproj, API, options, contributor, package README), its test project in the slnx, the sample wired to it (`Program.cs`, the `ProjectReference`, plus the local-feed `PackageReference` and the version entry in `Directory.Packages.props`), and SPEC/PLAN corrections if the design changed on the way.

12. **Design**: SPEC (contract for v0.2) and this plan. No code.
13. **Environment `Process` group**: `Id`, `StartTime` (`null` with `Uptime` when the start time is unknown) and `Uptime` (moved from `ApplicationProcessUptime`, a breaking change), tests, README of the package.
14. **Runtime package** (`Dut00.AppInfo.Runtime`): the simplest new package; it follows the typed-group pattern set by `Process` in step 13.
15. **Build package** (`Dut00.AppInfo.Build`): precedence rules, options, CI stamping examples in its README. Locally the commit comes from the SDK's `+<sha>` suffix; the sample also stamps `<AssemblyMetadata Include="BuildId" Value="local" />` to show the mechanism.
16. **Container package** (`Dut00.AppInfo.Container`): detection rules with fake host access.
17. **Server package** (`Dut00.AppInfo.Server`): `Urls`; check by hand with `dotnet run` that Kestrel reports the sample's address.
18. **Documentation and release prep**: root README (packages table, install commands, API table, example response, security note), `CLAUDE.md` layout, the code-reviewer checklist (SPEC §7, §8), SPEC status, `AppInfoPackageVersion` = `0.2.0`. Then pack and verify 8 packages; release by tagging `v0.2.0-preview.1` (the Trusted Publishing pattern `Dut00.AppInfo*` already covers the new IDs).

## Verification
1. `dotnet build -c Release` builds green for net8.0, net9.0 and net10.0. SDK 10 can build net9.0 without the net9 runtime.
2. `dotnet test` passes on net8.0 and net10.0.
3. `dotnet run --project samples/Dut00.AppInfo.Sample.Api`, then `curl http://localhost:5080/appinfo`: the JSON matches the draft and the password is masked. After v0.2 it has `Process` instead of `ApplicationProcessUptime`, and also `Build` (stamped commit), `Runtime`, `Container` (`IsContainer: false` locally) and `Urls` with `http://localhost:5080`.
4. `pwsh ./build/pack.ps1` produces 4 `.nupkg` and 4 `.snupkg` files in `./artifacts` (8 + 8 after v0.2). Inspect them for lib/net8.0, net9.0 and net10.0, the README, and the dependency on Dut00.AppInfo.
5. `dotnet run --project samples/Dut00.AppInfo.Sample.Api -p:UseLocalPackages=true`: the sample runs against the local-feed packages.

## Out of scope for v1
Dut00.AppInfo.Serilog (`Serilog:WriteTo` section with masking) was planned for after v1, then dropped. (CI, Dependabot and the nuget.org release workflow were added after v1.)
