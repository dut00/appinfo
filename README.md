# AspNetCore.AppInfo

[![CI](https://github.com/dut00/aspnetcore-appinfo/actions/workflows/ci.yml/badge.svg)](https://github.com/dut00/aspnetcore-appinfo/actions/workflows/ci.yml)

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
| `AspNetCore.AppInfo` | The endpoint, `ApplicationName`, `Version`, `Environment`, `IsProduction`, `Owner`, custom properties and the extensibility API |
| `AspNetCore.AppInfo.Configuration` | `ConfigurationsFiles`: the configuration files the app loaded |
| `AspNetCore.AppInfo.Environment` | `ApplicationProcessUptime`, `HostName`, `ContentRootPath`, `AssemblyLocation` |
| `AspNetCore.AppInfo.ConnectionStrings` | `ConnectionStrings`, with passwords, keys and other secrets masked |

All packages target `net8.0`, `net9.0` and `net10.0` and share one version. They are not on nuget.org yet; see [Local packages](#local-packages).

## Quick start

```csharp
using AspNetCore.AppInfo;
using AspNetCore.AppInfo.Configuration;
using AspNetCore.AppInfo.ConnectionStrings;
using AspNetCore.AppInfo.Environment;

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
- `AspNetCore.AppInfo.ConnectionStrings` always masks, and masking **fails closed**: a value it can't fully understand (URI-style, unparseable, Redis-style, split ODBC braces) is replaced completely with the mask (`***` by default). See the [package README](src/AspNetCore.AppInfo.ConnectionStrings/README.md) for the rules and known limitations. Masking reduces risk; it doesn't make the endpoint safe to expose publicly.

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
samples/    AspNetCore.AppInfo.Sample.Api, a minimal API using all packages
build/      pack.ps1
.github/    CI workflow (Linux, Windows, macOS) and Dependabot
docs/       SPEC.md (behavioral contract), PLAN.md (implementation plan)
```

```bash
dotnet build AspNetCore.AppInfo.slnx
dotnet test --solution AspNetCore.AppInfo.slnx
dotnet run --project samples/AspNetCore.AppInfo.Sample.Api   # then open http://localhost:5080/appinfo
```

The SDK version is pinned in `global.json`. Tests use Microsoft.Testing.Platform, which is why `dotnet test` takes `--solution`.

### Local packages

```bash
pwsh ./build/pack.ps1        # Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File build/pack.ps1
dotnet run --project samples/AspNetCore.AppInfo.Sample.Api -p:UseLocalPackages=true
```

`pack.ps1` writes the packages to `./artifacts`, which `nuget.config` registers as a local feed, and clears their cached copies so the next restore picks up the new build. With `-p:UseLocalPackages=true` the sample uses those packages instead of project references. To use them in another project, add `./artifacts` as a package source.

## Roadmap

- `AspNetCore.AppInfo.Serilog`: the configured Serilog sinks (`Serilog:WriteTo`), with secrets masked.
- Publishing to nuget.org.

## License

[MIT](LICENSE)
