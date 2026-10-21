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
  "Process": {
    "Id": 4711,
    "StartTime": "2026-10-10T08:12:03.4512345Z",
    "Uptime": "10.09:34:52.5222809"
  },
  "HostName": "apps-host-01",
  "ContentRootPath": "/opt/app-root",
  "AssemblyLocation": "/opt/app-root/Company.Billing.Api.dll"
}
```

| Key | Source |
| --- | --- |
| `Process.Id` | `System.Environment.ProcessId` |
| `Process.StartTime` | `Process.StartTime` in UTC (round-trip format, `...Z`), or `null` when the platform doesn't expose it |
| `Process.Uptime` | Time since the process started, computed on every request (`[d.]hh:mm:ss[.fffffff]`), or `null` when the start time is unknown |
| `HostName` | `System.Environment.MachineName` |
| `ContentRootPath` | `IHostEnvironment.ContentRootPath` |
| `AssemblyLocation` | Entry assembly location, or `null` when it has none (for example a single-file app) |

Since 0.2, the uptime is `Process.Uptime`; the 0.1 root key `ApplicationProcessUptime` is gone.

> These values describe your infrastructure. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
