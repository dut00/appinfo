# Dut00.AppInfo.Runtime

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with the .NET runtime, operating system and architectures the application runs on.

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Runtime;

builder.Services.AddAppInfo()
    .WithRuntimeDetails();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key:

```json
{
  "Runtime": {
    "Framework": ".NET 10.0.0",
    "RuntimeIdentifier": "linux-x64",
    "OSDescription": "Ubuntu 24.04.1 LTS",
    "OSArchitecture": "X64",
    "ProcessArchitecture": "X64"
  }
}
```

| Key | Source |
| --- | --- |
| `Runtime.Framework` | `RuntimeInformation.FrameworkDescription` |
| `Runtime.RuntimeIdentifier` | `RuntimeInformation.RuntimeIdentifier` |
| `Runtime.OSDescription` | `RuntimeInformation.OSDescription` |
| `Runtime.OSArchitecture` | `RuntimeInformation.OSArchitecture` (`X64`, `Arm64`, ...) |
| `Runtime.ProcessArchitecture` | `RuntimeInformation.ProcessArchitecture`, which differs from the OS architecture under emulation (for example an x64 process on Arm64) |

The values are computed once, because they can't change while the process runs.

> Exact versions help an attacker pick known vulnerabilities. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
