# Dut00.AppInfo.Environment

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with details about the process and the host it runs on.

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Environment;

builder.Services.AddAppInfo()
    .WithEnvironmentDetails();

var app = builder.Build();
app.MapAppInfo();
```

Adds these keys:

```json
{
  "ApplicationProcessUptime": "10.09:34:52.5222809",
  "HostName": "apps-host-01",
  "ContentRootPath": "/opt/app-root",
  "AssemblyLocation": "/opt/app-root/Company.Billing.Api.dll"
}
```

| Key | Source |
| --- | --- |
| `ApplicationProcessUptime` | Time since the process started, computed on every request (`[d.]hh:mm:ss[.fffffff]`) |
| `HostName` | `System.Environment.MachineName` |
| `ContentRootPath` | `IHostEnvironment.ContentRootPath` |
| `AssemblyLocation` | Entry assembly location, or `null` when it has none (for example a single-file app) |

> These values describe your infrastructure. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
