# Dut00.AppInfo

Adds an `/appinfo` endpoint to ASP.NET Core applications. It returns JSON describing the running application.

```csharp
using Dut00.AppInfo;

builder.Services.AddAppInfo()
    .WithOwner(o => o.Owner = "red_team@company.com")
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
| `WithOwner(o => o.Owner = ...)` | `Owner` |
| `WithProperty(key, value)` | A custom key with a constant value |
| `WithProperty(key, sp => ...)` | A custom key computed on every request; the factory may resolve scoped services |
| `WithContributor<T>()` | Everything a custom `IAppInfoContributor` writes |
| `MapAppInfo(pattern = "/appinfo")` | The `GET` endpoint; returns `IEndpointConventionBuilder` |

To write a constant `null`, cast it: `WithProperty("Key", (object?)null)`. A plain `null` binds to the factory overload and throws.

Keys appear in registration order. Use `o.KeyNamingPolicy = JsonNamingPolicy.CamelCase` (or any `JsonNamingPolicy`) to change the key format; `o.JsonSerializerOptions` gives full control over value serialization.

More fields come from the extension packages: `Dut00.AppInfo.Configuration`, `Dut00.AppInfo.Environment` and `Dut00.AppInfo.ConnectionStrings`.

## Custom contributors

```csharp
public sealed class GitInfoContributor : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Commit", System.Environment.GetEnvironmentVariable("GIT_COMMIT"));
        return ValueTask.CompletedTask;
    }
}

builder.Services.AddAppInfo().WithContributor<GitInfoContributor>();
```

> The endpoint is open by default and may expose internal details. Protect it, for example with `app.MapAppInfo().RequireAuthorization()`.
