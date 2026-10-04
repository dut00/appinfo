# <img src="assets/icon.png" alt="" width="48" height="48" align="absmiddle"> Dut00.AppInfo

[![CI](https://github.com/dut00/aspnetcore-appinfo/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/dut00/aspnetcore-appinfo/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Dut00.AppInfo?label=NuGet)](https://www.nuget.org/packages/Dut00.AppInfo)

> **Friday, 4:47 PM.** Invoices on Stage are coming out with last month's prices. QA opens a ticket against `billing-api`.
>
> The pipeline says version 2.3.1 went out on Tuesday, and that version fixed exactly this bug. So why is it back? Someone on the team guesses the deploy failed on one of the three hosts. Someone else thinks Stage is reading the wrong `appsettings` file. A third person remembers that last month somebody "temporarily" pointed Stage at a copy of the production database, but nobody knows whether that was ever reverted. Billing is one of forty-odd services, each with its own version on Dev, Stage and Prod, half on Kubernetes and half on old VMs. The last person who knew who owns which service left in spring. So people SSH into machines, read logs and post *"does anyone know who owns billing-api?"* on Slack.
>
> Two hours later the answer turns up: host `apps-host-02` was still running 2.2.9, `appsettings.Stage.json` was missing there, and the connection string pointed at `prod-db-copy`.
>
> With `/appinfo`, it takes one request:
>
> ```
> curl https://apps-host-02/appinfo
> ```
>
> `"Version": "2.2.9.0"`, `"ConfigurationsFiles"` with only `appsettings.json`, `"ConnectionStrings"` showing `Data Source=prod-db-copy;...;Password=***`, and `"Owner": "billing-team@company.com"`. That's everything, in one JSON, without logging in anywhere. Five minutes instead of two hours, and nobody had to look at a password to get there.

A family of NuGet packages that adds an `/appinfo` endpoint to ASP.NET Core applications. Like `/healthz` for liveness, it gives operators and tools one well-known address that answers: *which application and version runs here, in which environment, who owns it, and how is it configured?*

```json
{
  "ApplicationName": "Company.Billing.Api",
  "Version": "1.0.5.0",
  "Environment": "Stage",
  "IsProduction": false,
  "ConfigurationsFiles": [
    "/opt/app-root/appsettings.json",
    "/opt/app-root/appsettings.Stage.json"
  ],
  "ApplicationProcessUptime": "10.09:34:52.5222809",
  "HostName": "apps-host-01",
  "ContentRootPath": "/opt/app-root",
  "AssemblyLocation": "/opt/app-root/Company.Billing.Api.dll",
  "ConnectionStrings": {
    "Billing": "Data Source=db.company.com;Initial Catalog=Billing;User ID=***;Password=***"
  },
  "Owner": "red_team@company.com",
  "Team": "red"
}
```

## Packages

| Package | Adds |
| --- | --- |
| `Dut00.AppInfo` | The endpoint, `ApplicationName`, `Version`, `Environment`, `IsProduction`, `Owner`, custom properties and the extensibility API |
| `Dut00.AppInfo.Configuration` | `ConfigurationsFiles`: the configuration files the app loaded |
| `Dut00.AppInfo.Environment` | `ApplicationProcessUptime`, `HostName`, `ContentRootPath`, `AssemblyLocation` |
| `Dut00.AppInfo.ConnectionStrings` | `ConnectionStrings`, with passwords, keys and other secrets masked |

All packages target `net8.0`, `net9.0` and `net10.0` and share one version. They are not on nuget.org yet; see [Local packages](#local-packages).

## Quick start

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Configuration;
using Dut00.AppInfo.ConnectionStrings;
using Dut00.AppInfo.Environment;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppInfo()
    .WithConfigurationDetails()
    .WithEnvironmentDetails()
    .WithConnectionStrings()
    .WithOwner(o => o.Owner = "red_team@company.com")
    .WithProperty("Team", "red");

var app = builder.Build();

app.MapAppInfo(); // GET /appinfo. Open by default: protect it, see Security.

app.Run();
```

## API

| Member | Package | Adds |
| --- | --- | --- |
| `AddAppInfo(o => ...)` | core | Services and the core fields. Safe to call more than once. |
| `WithOwner(o => o.Owner = ...)` | core | `Owner` |
| `WithProperty(key, value)` | core | A custom key with a constant value. For a constant `null`, write `(object?)null`. |
| `WithProperty(key, sp => ...)` | core | A custom key computed on every request. The factory may resolve scoped services. |
| `WithContributor<T>()` | core | Everything a custom `IAppInfoContributor` writes |
| `WithConfigurationDetails(o => ...)` | Configuration | `ConfigurationsFiles`. `o.IncludeMissingOptionalFiles` also lists optional files that don't exist. |
| `WithEnvironmentDetails()` | Environment | Process and host details. The uptime uses the app's `TimeProvider` if one is registered. |
| `WithConnectionStrings(o => ...)` | ConnectionStrings | Masked `ConnectionStrings`. `o.SensitiveKeys` and `o.Mask` change what is masked and how. |
| `MapAppInfo(pattern = "/appinfo")` | core | The `GET` endpoint. Returns `IEndpointConventionBuilder`. |

- Keys appear in registration order: the core fields first, then each `With*` call in the order it was made. If two sources set the same key, the later one wins and a warning is logged.
- Keys are PascalCase by default. `AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)` changes the key format (any `JsonNamingPolicy` works). `o.JsonSerializerOptions` controls how values are serialized.
- `MapAppInfo` can be called more than once with different patterns.

## Security

**The endpoint is open by default.** It reveals file paths, host names and infrastructure addresses, so protect it like any internal endpoint. `MapAppInfo` returns an `IEndpointConventionBuilder`, so the usual conventions apply. For example, require an authorization policy:

```csharp
builder.Services.AddAuthentication(/* your scheme */);
builder.Services.AddAuthorization(o => o.AddPolicy("Ops", p => p.RequireRole("ops")));
// ...
app.MapAppInfo().RequireAuthorization("Ops");
```

Or answer only on an internal port:

```csharp
app.MapAppInfo().RequireHost("*:8081");
```

- The core package never emits secrets on its own. Values you pass to `WithProperty` are written as-is.
- `Dut00.AppInfo.ConnectionStrings` always masks, and masking **fails closed**: a value it can't fully understand (URI-style, unparseable, Redis-style, split ODBC braces) is replaced completely with the mask (`***` by default). See the [package README](src/Dut00.AppInfo.ConnectionStrings/README.md) for the rules and known limitations. Masking reduces risk; it doesn't make the endpoint safe to expose publicly.

## Custom contributors

A contributor adds keys to the response. It is resolved from the request's services, so it may depend on scoped services.

```csharp
public sealed class BuildInfoContributor(IConfiguration configuration) : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Commit", configuration["Build:Commit"]);
        context.Set("Pipeline", configuration["Build:Pipeline"]);
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddAppInfo().WithContributor<BuildInfoContributor>();
```

To ship a contributor as a package, add a `With*` extension method on `IAppInfoBuilder` that calls `WithContributor<T>()`. Put it in your package's own namespace, as the packages in this repo do.

## Repository

```
src/        the four packages, each with its own README
test/       xUnit v3 + Shouldly tests; integration tests run on TestServer
samples/    Dut00.AppInfo.Sample.Api, a minimal API using all packages
build/      pack.ps1
.github/    CI (Linux, Windows, macOS), release to nuget.org, Dependabot
assets/     icon.png, the package icon
docs/       SPEC.md (behavioral contract), PLAN.md (implementation plan)
```

```bash
dotnet build Dut00.AppInfo.slnx
dotnet test --solution Dut00.AppInfo.slnx
dotnet run --project samples/Dut00.AppInfo.Sample.Api   # then open http://localhost:5080/appinfo
```

The SDK version is pinned in `global.json`. Tests use Microsoft.Testing.Platform, which is why `dotnet test` takes `--solution`.

### Local packages

```bash
pwsh ./build/pack.ps1        # Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File build/pack.ps1
dotnet run --project samples/Dut00.AppInfo.Sample.Api -p:UseLocalPackages=true
```

`pack.ps1` writes the packages to `./artifacts`, which `nuget.config` registers as a local feed, and clears their cached copies so the next restore picks up the new build. With `-p:UseLocalPackages=true` the sample uses those packages instead of project references. To use them in another project, add `./artifacts` as a package source.

### Releasing

Releases are published to nuget.org by `.github/workflows/release.yml` when a version tag is pushed. It uses nuget.org Trusted Publishing, so no API key is stored in the repo.

```bash
git tag v0.1.0-preview.1
git push origin v0.1.0-preview.1
```

The workflow tests, packs with the tag's version, pushes the packages and symbols to nuget.org and creates a GitHub release. A tag with a suffix (`-preview.1`, `-rc.1`) becomes a prerelease. **The tag decides the published version**; `AppInfoPackageVersion` in `Directory.Build.props` is only the default for local packs.

One-time setup:

1. **nuget.org** > Trusted Publishing > add a policy: Repository Owner `dut00`, Repository `aspnetcore-appinfo`, Workflow File `release.yml` (file name only), Environment `nuget`. Its scope must allow **pushing new packages** (for example the pattern `Dut00.AppInfo*`), because the first release creates the package IDs.
2. **GitHub** > Settings > Environments > `nuget`: add a required reviewer. Under "Deployment branches and tags", choose "No restriction" or add a tag rule `v*`; otherwise tag-triggered runs are blocked.
3. **GitHub** > Settings > Secrets and variables > Actions > Variables: `NUGET_USER` = the nuget.org profile name (not the e-mail).
4. Recommended: a tag ruleset (Settings > Rules) that restricts creating, updating and deleting `v*` tags.

## Roadmap

- `Dut00.AppInfo.Serilog`: the configured Serilog sinks (`Serilog:WriteTo`), with secrets masked.

## License

[MIT](LICENSE)
