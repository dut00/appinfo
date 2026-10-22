# Dut00.AppInfo

Adds an `/appinfo` endpoint to ASP.NET Core applications. It returns JSON describing the running application.

```csharp
using Dut00.AppInfo;

builder.Services.AddAppInfo()
    .WithOwner("red_team@company.com")
    .WithProperty("Team", "red")
    .WithProperty("StartedBy", sp => sp.GetRequiredService<IMyService>().User);

var app = builder.Build();
app.MapAppInfo(); // GET /appinfo
```

```json
{
  "ApplicationName": "Company.Billing.Api",
  "Version": "1.0.5.0",
  "Environment": "Stage",
  "IsProduction": false,
  "Owner": "red_team@company.com",
  "Team": "red",
  "StartedBy": "deploy-bot"
}
```

## API

| Member | Adds |
| --- | --- |
| `AddAppInfo(o => ...)` | `ApplicationName`, `Version` (entry assembly), `Environment`, `IsProduction` |
| `WithOwner(owner)` | `Owner` |
| `WithProperty(key, value)` | A custom key with a constant value |
| `WithProperty(key, sp => ...)` | A custom key computed on every request; the factory may resolve scoped services |
| `WithContributor<T>()` | Everything a custom `IAppInfoContributor` writes |
| `MapAppInfo(pattern = "/appinfo")` | The `GET` endpoint; returns `IEndpointConventionBuilder` |

To write a constant `null`, cast it: `WithProperty("Key", (object?)null)`. A plain `null` binds to the factory overload and throws.

Keys appear in registration order. Use `o.KeyNamingPolicy = JsonNamingPolicy.CamelCase` (or any `JsonNamingPolicy`) to change the key format; `o.JsonSerializerOptions` gives full control over value serialization.

Characters such as `+`, `<`, `'` and non-ASCII letters are written as-is (`"1.0.5+a1b2c3d"`, not `"1.0.5\u002Ba1b2c3d"`). For the strict escaping of System.Text.Json, set `o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Default`.

Native AOT and trimming aren't supported yet: values are serialized with reflection-based System.Text.Json.

More fields come from the extension packages: `Dut00.AppInfo.Configuration`, `Dut00.AppInfo.Environment`, `Dut00.AppInfo.ConnectionStrings`, `Dut00.AppInfo.Build`, `Dut00.AppInfo.Runtime`, `Dut00.AppInfo.Container` and `Dut00.AppInfo.Server`.

## Custom contributors

```csharp
public sealed class RegionContributor : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Region", System.Environment.GetEnvironmentVariable("REGION"));
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddAppInfo().WithContributor<RegionContributor>();
```

> The endpoint is open by default and may expose internal details (responses carry `Cache-Control: no-store`). Protect it, for example with `app.MapAppInfo().RequireAuthorization()`.
