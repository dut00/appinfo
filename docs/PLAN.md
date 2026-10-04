# Plan: AspNetCore.AppInfo – NuGet package family exposing an `/appinfo` endpoint

## Context
The repo `C:\CODE\appinfo-nuget` currently contains only `draft.md` (Polish notes). Goal: a family of NuGet packages, structured like Xabaril/AspNetCore.Diagnostics.HealthChecks, that adds an `/appinfo` endpoint to ASP.NET Core apps. The endpoint returns JSON describing the application (name, version, environment, owner, …) and can be extended through separate packages.

**All code, comments, XML docs, READMEs and this plan are written in English.**

Naming: packages use the `AspNetCore.AppInfo.*` prefix (free on nuget.org, mirrors `AspNetCore.HealthChecks.*`), GitHub repo `dut00/aspnetcore-appinfo`. The original notes used `Kudu.AppInfo`.

The behavioral contract (public API, JSON fields, masking rules, edge cases) is defined in [SPEC.md](SPEC.md). If this plan and SPEC.md disagree, SPEC.md wins.

Agreed decisions:
- Target frameworks: `net8.0;net9.0;net10.0`
- Packages: core + separate extension packages. v1 ships **Configuration, Environment and ConnectionStrings (masked)**. Serilog comes later.
- Architecture: contributors implementing `IAppInfoContributor`, registered in DI. API: `AddAppInfo()` returns `IAppInfoBuilder`, then `.WithXxx()`. Endpoint: `MapAppInfo(pattern = "/appinfo")`.
- Core fields: ApplicationName, Version, Environment, IsProduction, plus `WithOwner` and custom fields (`WithProperty`)
- Flat JSON: extension fields sit at the root. Key casing is configurable, PascalCase by default.
- Security: `MapAppInfo` returns `IEndpointConventionBuilder`, so the user can chain `.RequireAuthorization()` etc. The README carries a warning.
- Layout: `src/ test/ samples/`, `.slnx`, `Directory.Build.props`, Central Package Management
- Tests: xUnit + Shouldly, unit and integration (TestServer)
- Local dev: the sample uses ProjectReference, and a pack script builds a local feed. All packages share one version.
- Workflow: **after each implementation step, stop and ask the user whether to commit. Never commit automatically.**

## Repository layout
```
appinfo-nuget/
├─ AspNetCore.AppInfo.slnx
├─ Directory.Build.props        # TFMs, LangVersion, Nullable, ImplicitUsings, TreatWarningsAsErrors,
│                               # NuGet metadata (Authors, MIT license, RepositoryUrl, Version=0.1.0,
│                               # PackageReadmeFile, GenerateDocumentationFile, Deterministic, snupkg symbols)
├─ Directory.Packages.props     # CPM: xunit.v3 (Microsoft.Testing.Platform, no VSTest packages),
│                               # Shouldly, Microsoft.AspNetCore.TestHost (versions per TFM)
├─ global.json                  # SDK 10.0.x, rollForward latestFeature, test runner = Microsoft.Testing.Platform
├─ nuget.config                 # nuget.org + local ./artifacts source
├─ .gitignore, .editorconfig, README.md, LICENSE
├─ docs/PLAN.md                 # copy of this plan
├─ docs/draft.md                # original notes (moved from root)
├─ build/pack.ps1               # dotnet pack -c Release -o ./artifacts (+ clears aspnetcore.appinfo* from NuGet cache)
├─ src/
│  ├─ AspNetCore.AppInfo/
│  ├─ AspNetCore.AppInfo.Configuration/
│  ├─ AspNetCore.AppInfo.Environment/
│  └─ AspNetCore.AppInfo.ConnectionStrings/
├─ test/
│  ├─ Directory.Build.props     # IsPackable=false, TFMs net8.0;net10.0 (only these runtimes are installed)
│  ├─ AspNetCore.AppInfo.Tests/
│  ├─ AspNetCore.AppInfo.Configuration.Tests/
│  ├─ AspNetCore.AppInfo.Environment.Tests/
│  └─ AspNetCore.AppInfo.ConnectionStrings.Tests/
└─ samples/
   └─ AspNetCore.AppInfo.Sample.Api/  # minimal API, ProjectReference to all packages
```

## Core design: `src/AspNetCore.AppInfo`
Depends only on `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.

- `IAppInfoContributor`:
  `ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)`
- `AppInfoContext` exposes `IServiceProvider Services`, `HttpContext HttpContext` and `void Set(string key, object? value)`. It is backed by a dictionary that keeps insertion order. If two contributors write the same key, the later one wins and a warning is logged.
- `IAppInfoBuilder { IServiceCollection Services; }`, implemented by `AppInfoBuilder`
- `AppInfoOptions`:
  - `JsonSerializerOptions JsonSerializerOptions`. Defaults: `WriteIndented = true`, `PropertyNamingPolicy = null`, `DictionaryKeyPolicy = null` (PascalCase).
  - `JsonNamingPolicy? KeyNamingPolicy`, a convenience property that sets both policies. Both are needed because the root object is a dictionary.
- `AddAppInfo(this IServiceCollection, Action<AppInfoOptions>? configure = null)` registers the options and `CoreAppInfoContributor` (via `TryAddEnumerable`) and returns the builder.
- `CoreAppInfoContributor` writes:
  - `ApplicationName` from `IHostEnvironment.ApplicationName`
  - `Version` from `Assembly.GetEntryAssembly()?.GetName().Version`
  - `Environment` from `EnvironmentName`
  - `IsProduction` from `IsProduction()`
- Builder extensions in core:
  - `WithOwner(Action<OwnerOptions>)` adds the `Owner` key.
  - `WithProperty(string key, object? value)` and `WithProperty(string key, Func<IServiceProvider, object?> factory)` add custom fields through `DelegateAppInfoContributor`.
  - `WithContributor<T>() where T : class, IAppInfoContributor` is the hook for third-party extensions.
- `MapAppInfo(this IEndpointRouteBuilder, string pattern = "/appinfo")`:
  - registers a `MapGet` that runs every `IEnumerable<IAppInfoContributor>` in registration order;
  - returns `Results.Json(dict, options.JsonSerializerOptions)`;
  - returns `IEndpointConventionBuilder` and sets the display name `AppInfo` (not `WithName`, which must be globally unique and would break mapping twice).
- Each package has its own `README.md` (PackageReadmeFile).

Namespaces match package names (`AspNetCore.AppInfo`, `AspNetCore.AppInfo.Environment`, …), and each package's extension methods live in its namespace, as in the draft (`using AspNetCore.AppInfo.Environment;`). Because that namespace collides with `System.Environment`, the Environment project must write `System.Environment` in full.

## Extensions
Each extension is a separate csproj with a ProjectReference to core (packed as a NuGet dependency), one contributor class and one extension-method class.

- **AspNetCore.AppInfo.Configuration**: `WithConfigurationDetails()` adds `ConfigurationsFiles: string[]`.
  - Source: `IConfiguration as IConfigurationRoot`, then `Providers.OfType<FileConfigurationProvider>()`, then `Source.FileProvider?.GetFileInfo(Source.Path).PhysicalPath ?? Source.Path`.
  - Only files that exist are listed by default. The `IncludeMissingOptionalFiles` option includes missing optional files too.
- **AspNetCore.AppInfo.Environment**: `WithEnvironmentDetails()` adds four keys:
  - `ApplicationProcessUptime`: process start time to now, as a TimeSpan. Computed on each request through an injected `TimeProvider` so tests can control it.
  - `HostName`: `System.Environment.MachineName`
  - `ContentRootPath`: from `IHostEnvironment`
  - `AssemblyLocation`: `Assembly.GetEntryAssembly()?.Location`
- **AspNetCore.AppInfo.ConnectionStrings**: `WithConnectionStrings(Action<ConnectionStringsOptions>? configure = null)` adds `ConnectionStrings: { "<name>": "<masked value>" }`, read from `IConfiguration.GetSection("ConnectionStrings")`.
  - Masking parses each value with `DbConnectionStringBuilder`. Keys listed in `SensitiveKeys` get the value `***`.
  - Default `SensitiveKeys` (case-insensitive): Password, Pwd, User ID, UID, User, Username, AccountKey, SharedAccessKey, SharedAccessSignature, AccessKey, Secret, Token, ApiKey.
  - If a value can't be parsed, the whole value becomes `***`.
  - Both `SensitiveKeys` and the mask string can be changed in options.

## Sample: `samples/AspNetCore.AppInfo.Sample.Api`
- `Program.cs` follows the draft: `AddAppInfo().WithConfigurationDetails().WithEnvironmentDetails().WithConnectionStrings().WithOwner(...).WithProperty("Team", "red")`, then `app.MapAppInfo()`.
- `appsettings.json` contains a sample connection string with a password, to show masking.
- `launchSettings.json` opens `/appinfo`.
- The sample uses ProjectReference by default. Building with `-p:UseLocalPackages=true` switches to PackageReference against `./artifacts` (conditional ItemGroup in the csproj).

## Tests (xUnit + Shouldly)
- Shared helper per test project: `WebApplication.CreateBuilder()` + `builder.WebHost.UseTestServer()`, in-memory configuration, then `GetAppInfoAsync()` (a parsed `JsonElement`) or `GetAppInfoStringAsync()` (the raw body).
- **Core** covers:
  - default path and custom path;
  - core fields, `WithOwner`, and `WithProperty` (value and factory);
  - key order and key overwrite;
  - `KeyNamingPolicy = CamelCase` changes the keys;
  - `.RequireAuthorization()` makes the endpoint return 401 (proves conventions apply);
  - a custom `IAppInfoContributor`.
- **Configuration**: json files in a temp directory; full paths appear in the output; a missing optional file is left out.
- **Environment**: fields are present with the right types; uptime is checked with a fake `TimeProvider`; HostName equals MachineName.
- **ConnectionStrings**: unit tests for the masker (SQL Server, PostgreSQL, Azure Storage, unparseable string, custom keys) plus one integration test.

## Implementation steps
After **every** step: run `dotnet build` and `dotnet test` (once tests exist), summarize the result, then **ask the user whether to commit**. Don't commit without approval.

1. **Repo bootstrap**: `git init`, `.gitignore` (dotnet template), `.editorconfig`, `global.json`, `LICENSE` (MIT), move `draft.md` to `docs/draft.md`, copy this plan to `docs/PLAN.md`, root `README.md` stub.
2. **Build infrastructure**: `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, empty `AspNetCore.AppInfo.slnx`.
3. **Core package skeleton**: `src/AspNetCore.AppInfo` csproj, `IAppInfoContributor`, `AppInfoContext`, `IAppInfoBuilder`, `AppInfoOptions`, `AddAppInfo`, `MapAppInfo`, `CoreAppInfoContributor`.
4. **Core builder extensions**: `WithOwner`, `WithProperty` (both overloads), `WithContributor<T>`, `DelegateAppInfoContributor`, `KeyNamingPolicy`.
5. **Core tests**: `test/Directory.Build.props`, `test/AspNetCore.AppInfo.Tests` with the TestServer helper and all core scenarios.
6. **Sample app**: `samples/AspNetCore.AppInfo.Sample.Api` using core only. Check by hand with `dotnet run` + `curl /appinfo`.
7. **Environment package + tests**, then add it to the sample.
8. **Configuration package + tests**, then add it to the sample.
9. **ConnectionStrings package + tests** (masker unit tests and an integration test), then add it to the sample.
10. **Packaging and local feed**: per-package READMEs, `build/pack.ps1`, the `UseLocalPackages` switch in the sample, then check the `.nupkg` contents.
11. **Documentation**: root `README.md` in English (quick start, every `With*` method, sample response, security warning, how to write a custom contributor, roadmap). Update `docs/PLAN.md` if anything changed during implementation.

## Verification
1. `dotnet build -c Release` builds green for net8.0, net9.0 and net10.0. SDK 10 can build net9.0 without the net9 runtime.
2. `dotnet test` passes on net8.0 and net10.0.
3. `dotnet run --project samples/AspNetCore.AppInfo.Sample.Api`, then `curl http://localhost:5080/appinfo`: the JSON matches the draft and the password is masked.
4. `./build/pack.ps1` produces 4 `.nupkg` and 4 `.snupkg` files in `./artifacts`. Inspect them for lib/net8.0, net9.0 and net10.0, the README, and the dependency on AspNetCore.AppInfo.
5. `dotnet run --project samples/AspNetCore.AppInfo.Sample.Api -p:UseLocalPackages=true`: the sample runs against the local-feed packages.

## Out of scope for v1 (listed in the README roadmap)
AspNetCore.AppInfo.Serilog (`Serilog:WriteTo` section with masking), CI (GitHub Actions), publishing to nuget.org.
