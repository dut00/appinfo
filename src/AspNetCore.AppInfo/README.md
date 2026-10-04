# AspNetCore.AppInfo

Adds an `/appinfo` endpoint to ASP.NET Core applications. It returns JSON describing the running application.

```csharp
builder.Services.AddAppInfo();

var app = builder.Build();
app.MapAppInfo(); // GET /appinfo
```

```json
{
  "ApplicationName": "Company.Billing.Api",
  "Version": "1.0.5.0",
  "Environment": "Stage",
  "IsProduction": false
}
```

> The endpoint is open by default and may expose internal details. Protect it, for example with `app.MapAppInfo().RequireAuthorization()`.
