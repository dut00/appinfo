# Dut00.AppInfo.ConnectionStrings

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with the application's connection strings, with secrets masked.

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.ConnectionStrings;

builder.Services.AddAppInfo()
    .WithConnectionStrings();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key, read from the `ConnectionStrings` configuration section:

```json
{
  "ConnectionStrings": {
    "Billing": "Data Source=db.company.com;Initial Catalog=Billing;User ID=***;Password=***"
  }
}
```

## How masking works

Each value is parsed with `DbConnectionStringBuilder`. Masking **fails closed**: when in doubt, the whole value becomes `***`.

- A key is masked when it **contains** a sensitive word, ignoring case. The defaults are `Password`, `Pwd`, `PSW`, `Pass`, `User ID`, `UID`, `User`, `Username`, `Key`, `AccountKey`, `SharedAccessKey`, `SharedAccessSignature`, `AccessKey`, `ApiKey`, `Secret`, `Token`, `Credential`, `Authorization`, `Signature`, `Bearer`. So `Password` also covers `Proxy Password`, `Key` covers `AppKey` and `private_key`, and short words also mask harmless keys such as `User Instance`.
- A value is masked when it hides a secret: a nested pair such as `Extended Properties="...;Password=xyz"`, anything with `@` (credentials such as `user:pass@host`), or an address with a query or fragment, such as a SAS URL in `BlobEndpoint`, even if it isn't a valid URI.
- Over-masking is accepted: for example `Application Name=app@prod` or SQLite's `Data Source=file:app.db?mode=memory&cache=shared` are masked too.
- The whole value is masked when it can't be parsed, is URI-style (`postgres://user:pass@host/db`, `mongodb://...`), isn't in `key=value;` format (for example a Redis string like `host:6379,password=...`), or has an ODBC braced value (`{...}`, with `}}` escapes) that was split by a `;` inside it.
- Not detected: a secret in the path of an address (such as a webhook token), or a secret under a key name no sensitive word covers (such as `Sas=` or `Cert=`). Add such keys to `SensitiveKeys`, or don't keep these values under `ConnectionStrings`.
- Empty values are shown as-is. Key casing and order are kept; spacing and quoting may be normalized.

Add your own sensitive words or change the mask:

```csharp
builder.Services.AddAppInfo()
    .WithConnectionStrings(o =>
    {
        o.SensitiveKeys.Add("Tenant");
        o.Mask = "<hidden>";
    });
```

> Masking reduces risk; it doesn't make the endpoint public-safe. Protect it, for example with `app.MapAppInfo().RequireAuthorization()`.
