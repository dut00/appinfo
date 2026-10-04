# AspNetCore.AppInfo.Configuration

Extends the [AspNetCore.AppInfo](https://www.nuget.org/packages/AspNetCore.AppInfo) `/appinfo` endpoint with the list of configuration files the application loaded.

```csharp
using AspNetCore.AppInfo;
using AspNetCore.AppInfo.Configuration;

builder.Services.AddAppInfo()
    .WithConfigurationDetails();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key:

```json
{
  "ConfigurationsFiles": [
    "/opt/app-root/appsettings.json",
    "/opt/app-root/appsettings.Stage.json"
  ]
}
```

- Lists every file-based configuration source (JSON, XML, INI, user secrets) in load order, including sources inside configurations added with `AddConfiguration(...)`.
- Paths are absolute when the file provider is physical; otherwise the configured path is shown.
- Optional files that don't exist (for example `appsettings.Production.json` in an app without one) are left out. Include them with:

```csharp
builder.Services.AddAppInfo()
    .WithConfigurationDetails(o => o.IncludeMissingOptionalFiles = true);
```

> File paths describe your servers. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
