# Dut00.AppInfo.Server

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with the addresses the server listens on: "which port is this instance on?"

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Server;

builder.Services.AddAppInfo()
    .WithServerAddresses();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key:

```json
{
  "Urls": [
    "http://[::]:8080"
  ]
}
```

- The addresses come from the server's `IServerAddressesFeature` (Kestrel, HTTP.sys, IIS) and are written as the server reports them, wildcards included (`http://[::]:8080`, `http://+:80`).
- They are read on every request. By then the server has started, so the list is final, including the actual port when you bind to port 0 (`http://127.0.0.1:0` is reported as, for example, `http://127.0.0.1:54321`).
- `Urls` is `[]` when the server reports no addresses (for example `TestServer`) and `null` when there is no server or it doesn't expose its addresses.
- Behind a reverse proxy or a container port mapping these are the addresses inside the host or container, not the public ones.

> Ports and bind addresses describe your infrastructure. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
