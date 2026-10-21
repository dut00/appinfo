# Dut00.AppInfo – Specification

Version: 0.2 (draft) · Status: v1 implemented; v0.2: the `Process` group, Runtime, Build, Container and Server are implemented; documentation and release prep remain (PLAN.md step 18)

This document defines **what** Dut00.AppInfo does: public API, response contract and behavior.
**How** it is built is described in [PLAN.md](PLAN.md). The original notes are in [draft.md](draft.md).

## 1. Purpose

Dut00.AppInfo adds an HTTP endpoint, `/appinfo` by default, to ASP.NET Core applications. It returns a JSON document describing the running application. The idea comes from the `/healthz` liveness probe convention: a standard, well-known address that operators and tools can query without knowing the application.

Typical questions it answers:
- Which application and version is running here?
- Which environment is it running in, and is it production?
- Who owns it?
- Which configuration files were loaded, and on which host?
- Which commit and build produced the running binary?
- Which .NET runtime, OS and architecture is it running on, inside which container or pod, and on which addresses does it listen?

## 2. Packages

| Package | Depends on | Adds |
| --- | --- | --- |
| `Dut00.AppInfo` | `Microsoft.AspNetCore.App` (framework reference) | Endpoint, core fields, owner, custom properties, extensibility API |
| `Dut00.AppInfo.Configuration` | `Dut00.AppInfo` | `ConfigurationsFiles` |
| `Dut00.AppInfo.Environment` | `Dut00.AppInfo` | `Process` (ID, start time, uptime), `HostName`, `ContentRootPath`, `AssemblyLocation` |
| `Dut00.AppInfo.ConnectionStrings` | `Dut00.AppInfo` | `ConnectionStrings` (masked) |
| `Dut00.AppInfo.Build` | `Dut00.AppInfo` | `Build`: informational version, commit, branch, build ID |
| `Dut00.AppInfo.Runtime` | `Dut00.AppInfo` | `Runtime`: .NET runtime, runtime identifier, OS, architectures |
| `Dut00.AppInfo.Container` | `Dut00.AppInfo` | `Container`: container and Kubernetes detection, pod, namespace, node |
| `Dut00.AppInfo.Server` | `Dut00.AppInfo` | `Urls`: the addresses the server listens on |

- Supported target frameworks: `net8.0`, `net9.0`, `net10.0`.
- All packages share one version number.
- No package depends on anything beyond `Dut00.AppInfo` and the ASP.NET Core shared framework.

## 3. Public API

### 3.1 Registration

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Build;
using Dut00.AppInfo.Configuration;
using Dut00.AppInfo.ConnectionStrings;
using Dut00.AppInfo.Container;
using Dut00.AppInfo.Environment;
using Dut00.AppInfo.Runtime;
using Dut00.AppInfo.Server;

builder.Services.AddAppInfo(options =>
    {
        options.KeyNamingPolicy = JsonNamingPolicy.CamelCase; // optional, default: PascalCase
    })
    .WithConfigurationDetails()
    .WithEnvironmentDetails()
    .WithConnectionStrings(o => o.SensitiveKeys.Add("ClientSecret"))
    .WithBuildDetails()
    .WithRuntimeDetails()
    .WithContainerDetails()
    .WithServerAddresses()
    .WithOwner("red_team@company.com")
    .WithProperty("Team", "red")
    .WithProperty("StartedBy", sp => sp.GetRequiredService<IMyService>().User);
```

| Member | Package | Description |
| --- | --- | --- |
| `IServiceCollection.AddAppInfo(Action<AppInfoOptions>? configure = null)` | core | Registers services and core fields. Returns `IAppInfoBuilder`. Safe to call more than once: core services register once, and every `configure` delegate is applied. |
| `IAppInfoBuilder.WithOwner(string owner)` | core | Adds `Owner`. A null, empty or whitespace `owner` throws `ArgumentException`. Safe to call more than once: the field is added once and the last value wins. |
| `IAppInfoBuilder.WithProperty(string key, object? value)` | core | Adds a custom key with a constant value. A plain `null` binds to the factory overload and throws; write `(object?)null` for a constant null. A delegate value (e.g. `() => x`) throws `ArgumentException`. |
| `IAppInfoBuilder.WithProperty(string key, Func<IServiceProvider, object?> factory)` | core | Adds a custom key whose value is computed on every request from the request's service provider. |
| `IAppInfoBuilder.WithContributor<T>()` | core | Registers a custom `IAppInfoContributor` as scoped. Registering the same type twice has no effect. |
| `IAppInfoBuilder.WithConfigurationDetails(Action<ConfigurationDetailsOptions>? configure = null)` | Configuration | Adds `ConfigurationsFiles`. |
| `IAppInfoBuilder.WithEnvironmentDetails()` | Environment | Adds the environment fields. |
| `IAppInfoBuilder.WithConnectionStrings(Action<ConnectionStringsOptions>? configure = null)` | ConnectionStrings | Adds masked `ConnectionStrings`. |
| `IAppInfoBuilder.WithBuildDetails(Action<BuildDetailsOptions>? configure = null)` | Build | Adds `Build`. See §7. |
| `IAppInfoBuilder.WithRuntimeDetails()` | Runtime | Adds `Runtime`. |
| `IAppInfoBuilder.WithContainerDetails(Action<ContainerDetailsOptions>? configure = null)` | Container | Adds `Container`. See §8. |
| `IAppInfoBuilder.WithServerAddresses()` | Server | Adds `Urls`. |

All `With*` methods return `IAppInfoBuilder` so calls can be chained. Each method lives in its package's namespace. Every `With*` method of an extension package is safe to call more than once: its contributor is registered once, and every `configure` delegate is applied. (`WithOwner` and `WithProperty` follow their own rules above.)

### 3.2 Endpoint

```csharp
app.MapAppInfo();                       // GET /appinfo
app.MapAppInfo("/custom-appinfo-path"); // GET /custom-appinfo-path
app.MapAppInfo().RequireAuthorization("Ops");
```

- `IEndpointRouteBuilder.MapAppInfo(string pattern = "/appinfo")` maps a `GET` endpoint with the display name `AppInfo`. It sets no endpoint name, so `MapAppInfo` can be called more than once with different patterns. Chain `.WithName(...)` to make it addressable for link generation.
- It returns `IEndpointConventionBuilder`, so every standard convention applies: `RequireAuthorization`, `RequireHost`, `RequireCors`, `CacheOutput`, and so on.
- Calling `MapAppInfo` without `AddAppInfo` throws `InvalidOperationException` with a message that tells the developer to call `AddAppInfo()`.

### 3.3 Extensibility

```csharp
public interface IAppInfoContributor
{
    ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken);
}

public sealed class AppInfoContext
{
    public HttpContext HttpContext { get; }
    public IServiceProvider Services { get; }
    public void Set(string key, object? value);
}
```

- Third parties extend AppInfo by implementing `IAppInfoContributor` and exposing a `With*` extension method on `IAppInfoBuilder` that calls `WithContributor<T>()`.
- Contributors are resolved from the request's service provider. `WithContributor<T>()` registers them as scoped, so they may depend on scoped services.
- Every `WithProperty` call adds its own key, even when the key repeats; the duplicate-key rule in §4.3 then applies.

## 4. Response contract

### 4.1 HTTP

| Aspect | Value |
| --- | --- |
| Method | `GET` |
| Success status | `200 OK` |
| Content-Type | `application/json; charset=utf-8` |
| Body | A single JSON object (see the shape rule below) |

Shape rule: a package that adds unrelated single values writes them as keys at the root (`HostName`, `Urls`). A package that adds a set of related values groups them under **one root key whose value is an object** (`Process`, `Build`, `Runtime`, `Container`). Inside a group, every property is always written, as `null` when unknown (subject to `JsonSerializerOptions.DefaultIgnoreCondition`).

Errors follow ASP.NET Core defaults:
- `401` / `403` when authorization conventions apply;
- `500` when a contributor throws. The exception propagates to the app's normal error handling.

### 4.2 Fields

| Key | Type | Package | Source | Notes |
| --- | --- | --- | --- | --- |
| `ApplicationName` | string | core | `IHostEnvironment.ApplicationName` | |
| `Version` | string \| null | core | Entry assembly `AssemblyName.Version` | e.g. `"1.0.5.0"` |
| `Environment` | string | core | `IHostEnvironment.EnvironmentName` | |
| `IsProduction` | bool | core | `IHostEnvironment.IsProduction()` | |
| `Owner` | string | core (`WithOwner`) | `WithOwner(owner)` | Left out unless `WithOwner` is called. Always a string: further owner details, if ever added, go in separate keys |
| *custom* | any JSON-serializable value | core (`WithProperty`) | user value or factory | |
| `ConfigurationsFiles` | string[] | Configuration | File-based configuration providers | Absolute physical paths, in load order |
| `Process` | object | Environment | The current process | `Id` (number, `System.Environment.ProcessId`); `StartTime` (string \| null: `Process.StartTime` as a preformatted UTC string in the round-trip format `"O"`, always 7 fraction digits and `Z`, e.g. `"2026-10-20T08:00:00.1234567Z"`, unaffected by custom JSON converters; `null` when the platform doesn't expose it); `Uptime` (string \| null, TimeSpan constant format `[d.]hh:mm:ss[.fffffff]`: now − start time, computed per request with the registered `TimeProvider`, `TimeProvider.System` by default; days and fractions are left out when zero; a clock behind the start time reports `00:00:00`; `null` when the start time is unknown). Replaces the v1 root key `ApplicationProcessUptime` |
| `HostName` | string | Environment | `System.Environment.MachineName` | |
| `ContentRootPath` | string | Environment | `IHostEnvironment.ContentRootPath` | |
| `AssemblyLocation` | string \| null | Environment | Entry assembly `Location` | `null` when there is none, for example in a single-file app |
| `ConnectionStrings` | object (name → masked string) | ConnectionStrings | `ConnectionStrings` configuration section | See §5 |
| `Build` | object | Build | See §7 | `InformationalVersion`, `Commit`, `Branch`, `BuildId`: each string \| null |
| `Runtime` | object | Runtime | `RuntimeInformation` | `Framework` (`FrameworkDescription`, e.g. `".NET 10.0.0"`), `RuntimeIdentifier` (e.g. `"linux-x64"`), `OSDescription`, `OSArchitecture` and `ProcessArchitecture` (enum names, e.g. `"X64"`, `"Arm64"`). All strings, computed once |
| `Container` | object | Container | See §8 | `IsContainer` and `IsKubernetes`: bool; `PodName`, `Namespace`, `NodeName`: string \| null |
| `Urls` | string[] \| null | Server | `IServerAddressesFeature.Addresses` of the registered `IServer` | Read per request, so the server has started and the addresses are final. As reported by the server, e.g. `"http://[::]:8080"`. An empty feature gives `[]` (`TestServer` has one); `null` only when there is no `IServer` or the server has no addresses feature |

### 4.3 Ordering and key rules

- The JSON keeps keys in the order contributors run: core fields first, then the `With*` calls in the order they were made. Within one contributor, keys keep the order in which they were set.
- If two contributors set the same key, the **later one wins** and a warning is logged under the `Dut00.AppInfo` category.
- Keys are case-sensitive. If two distinct keys end up with the same name after `KeyNamingPolicy` is applied (for example `Team` and `team` under CamelCase), the later one wins and the same warning is logged.
- A value of `null` is still written as `"Key": null`.

### 4.4 Naming policy

- By default, keys are emitted exactly as defined (PascalCase), matching the draft.
- `AppInfoOptions.KeyNamingPolicy` applies a `JsonNamingPolicy` (for example `CamelCase` or `SnakeCaseLower`) to root keys and to nested object properties.
- Keys of nested dictionaries are user data (for example connection string names) and are emitted as-is. `JsonSerializerOptions.DictionaryKeyPolicy` can still be set to convert them.
- For full control, `AppInfoOptions.JsonSerializerOptions` can be modified directly; it applies to values, not to root keys. Default: indented output.

### 4.5 Escaping

- The default `JsonSerializerOptions.Encoder` is `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so characters such as `+`, `<`, `>`, `&`, `'` and non-ASCII letters are written as-is (`"1.0.5+a1b2c3d"`, not `"1.0.5\u002Ba1b2c3d"`). Quotes, backslashes and control characters are still escaped, so the body is always valid JSON.
- "Unsafe" refers to embedding the text in HTML or JavaScript without further encoding. The endpoint answers `application/json` for people and tools, so readability wins. Setting `Encoder = JavaScriptEncoder.Default` restores the strict escaping.
- Escaping is a property of the whole response (root keys, values, nested objects and dictionaries), not of one package.
- Replacing `AppInfoOptions.JsonSerializerOptions` with a new instance discards the defaults (indentation and the relaxed encoder); modify it in place or set them again.

### 4.6 Example response (all packages)

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
  "Process": {
    "Id": 1,
    "StartTime": "2026-10-10T08:12:03.4512345Z",
    "Uptime": "10.09:34:52.5222809"
  },
  "HostName": "billing-api-7f9c6d5b4-x2kqp",
  "ContentRootPath": "/opt/app-root",
  "AssemblyLocation": "/opt/app-root/Company.Billing.Api.dll",
  "ConnectionStrings": {
    "Billing": "Data Source=db.company.com;Initial Catalog=Billing;User ID=***;Password=***"
  },
  "Build": {
    "InformationalVersion": "1.0.5+a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
    "Commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
    "Branch": "main",
    "BuildId": "1234"
  },
  "Runtime": {
    "Framework": ".NET 10.0.0",
    "RuntimeIdentifier": "linux-x64",
    "OSDescription": "Ubuntu 24.04.1 LTS",
    "OSArchitecture": "X64",
    "ProcessArchitecture": "X64"
  },
  "Container": {
    "IsContainer": true,
    "IsKubernetes": true,
    "PodName": "billing-api-7f9c6d5b4-x2kqp",
    "Namespace": "stage",
    "NodeName": "aks-node-3"
  },
  "Urls": [
    "http://[::]:8080"
  ],
  "Owner": "red_team@company.com",
  "Team": "red"
}
```

## 5. Connection string masking

Every value under `ConnectionStrings:*` that has a value of its own is masked. Nested sections (`ConnectionStrings:Name:Key`) are left out. Masking **fails closed**: when in doubt, the whole value is replaced with `Mask`.

1. An empty or whitespace value is written as-is.
2. The **whole value** is masked when:
   - it starts with a URI scheme (`postgres://`, `mongodb://`, `redis://`, ...), because URI-style strings are not key/value pairs (defense in depth: the key rule below catches these too);
   - `DbConnectionStringBuilder` can't parse it, or it has no keys;
   - any parsed key doesn't look like a connection string key (letters, digits, spaces, `.`, `-`, `_`). This catches formats such as Redis `host:6379,password=...`, which the builder would otherwise read as one odd key;
   - any parsed value that starts with `{` isn't a properly closed ODBC braced value (it must end with `}`, and no single `}` may remain inside once `}}` escapes are removed), or a value ends with `}` without starting with `{`. The builder doesn't treat braces as quoting, so `PWD={ab;cd=secret}` or `PWD={ab}};cd={secret}` would otherwise be split and print the second half;
   - anything else goes wrong while masking.
3. A **key's value** is masked when the key **contains** any entry of `SensitiveKeys`, ignoring case. `Password` therefore also covers `Proxy Password` and `SSL Password`, and `Key` covers `AppKey`, `Application Key` and `private_key`. Short entries also mask harmless keys such as `User Instance` or `KeyFile` (over-masking is accepted).
4. Any other value is masked when it hides a secret:
   - it contains a sensitive entry followed (after optional spaces or quotes) by `=` or `:`, i.e. a nested connection string such as `Extended Properties="Excel 12.0;Password=xyz"`;
   - it contains `@` anywhere: credentials in an address (`user:pass@host`, `user/pass@host`, `https://user:pass@host`, ...);
   - it contains `?` or `#` together with `/` or `:`: a query or fragment in an address, such as a SAS URL in `BlobEndpoint=https://...?sv=...&sig=...`;
   - it is an absolute URI with user info, a query or a fragment.
   - These rules work on the text, so addresses that aren't valid URIs are caught too. They over-mask some harmless values, for example `Application Name=app@prod`, `Host=hockey:5432` (contains `Key` + `:`) or SQLite's `Data Source=file:app.db?mode=memory&cache=shared`; that is accepted.
   - Known limitations: a secret in the **path** of an address (for example a webhook token in `https://host/api/webhooks/1/TOKEN`), or a secret under a key name that no `SensitiveKeys` entry covers (for example `Sas=sv=...&sig=...` or `Cert=-----BEGIN PRIVATE KEY-----`), is not detected. Add such keys to `SensitiveKeys`, or don't keep these values under `ConnectionStrings`. Unicode lookalikes of `@`, `=` or `/` are not treated as separators, matching how providers parse them.
5. The result is rebuilt from the parsed pairs. Key casing and order are kept (the original spelling of each key is restored only when it equals the parsed key ignoring case); spacing and quoting may be normalized. When a key appears twice, only the last value is kept, masked by the same rules.

| Option | Default |
| --- | --- |
| `SensitiveKeys` | `Password`, `Pwd`, `PSW`, `Pass`, `User ID`, `UID`, `User`, `Username`, `Key`, `AccountKey`, `SharedAccessKey`, `SharedAccessSignature`, `AccessKey`, `ApiKey`, `Secret`, `Token`, `Credential`, `Authorization`, `Signature`, `Bearer` (case-insensitive set, matched as substrings; blank entries are ignored) |
| `Mask` | `***` (must not be `null`) |

Connection string names are user data and are emitted as-is, regardless of `KeyNamingPolicy` (§4.4).

## 6. Configuration files detection

- The list includes every provider of type `FileConfigurationProvider` in the root `IConfiguration` (JSON, XML, INI, user secrets), in load order. Configurations added with `AddConfiguration(...)` (`ChainedConfigurationProvider`) are searched too, each root once.
- Paths are resolved to absolute physical paths through the provider's `IFileProvider`. A missing file under a `PhysicalFileProvider` gets the path where it would be. Otherwise (for example an embedded file provider) the configured path is used.
- By default, optional files that don't exist are left out. `ConfigurationDetailsOptions.IncludeMissingOptionalFiles = true` includes them. Required files are always listed, even if deleted after startup.
- The list is computed on every request. If `IConfiguration` is not an `IConfigurationRoot`, the list is empty.

## 7. Build details

`WithBuildDetails` writes `Build` with four properties. Each is resolved from the first source that has a non-empty value; whitespace around values is trimmed.

| Property | 1. Configuration | 2. Entry assembly | 3. Fallback | 4. Environment variables (defaults) |
| --- | --- | --- | --- | --- |
| `InformationalVersion` | – | `AssemblyInformationalVersionAttribute` | – | – |
| `Commit` | `Build:Commit` | `[AssemblyMetadata("Commit", ...)]` | From `InformationalVersion`: take everything after the first `+`, split it on `.`, and use the last segment when it consists solely of 7–64 hex digits. This covers `1.0.5+<sha>` and `1.0.5+meta.<sha>` (the SDK appends the git commit when it can find the repository, after a `.` if the version already has metadata), and SHA-256 repositories | `GIT_COMMIT`, `GITHUB_SHA`, `CI_COMMIT_SHA`, `BUILD_SOURCEVERSION` |
| `Branch` | `Build:Branch` | `[AssemblyMetadata("Branch", ...)]` | – | `GIT_BRANCH`, `GITHUB_REF_NAME`, `CI_COMMIT_REF_NAME`, `BUILD_SOURCEBRANCHNAME` |
| `BuildId` | `Build:BuildId` | `[AssemblyMetadata("BuildId", ...)]` | – | `BUILD_ID`, `GITHUB_RUN_ID`, `CI_PIPELINE_ID`, `BUILD_BUILDID` |

- Configuration comes first so a deployment can override what the binary says, for example with the environment variable `Build__Commit`. The section name is `BuildDetailsOptions.ConfigurationSection` (default `Build`).
- CI variables such as `GITHUB_SHA` exist while the CI job runs, not on the host that runs the app. The reliable source is the binary: stamp `AssemblyMetadata` at build time (`<AssemblyMetadata Include="Commit" Value="$(GITHUB_SHA)" />`). The environment variables are a last resort for deployments that pass them through. The lists are `BuildDetailsOptions.CommitEnvironmentVariables`, `BranchEnvironmentVariables` and `BuildIdEnvironmentVariables`, checked in order; blank entries are ignored.
- Only these named configuration keys and environment variables are read, never a whole section, so nothing else from configuration can leak.
- Assembly values are read once. Configuration and environment variables are read on every request.

## 8. Container detection

`WithContainerDetails` writes `Container`. Values are read once, on the first request.

| Property | Rule |
| --- | --- |
| `IsKubernetes` | The environment variable `KUBERNETES_SERVICE_HOST` is non-empty (Kubernetes injects it into every pod) |
| `IsContainer` | `IsKubernetes`, or `DOTNET_RUNNING_IN_CONTAINER` equals `true` ignoring case (set by the official .NET container images, Linux and Windows), or, on non-Windows only, `/.dockerenv` (Docker) or `/run/.containerenv` (Podman) exists |
| `PodName` | The environment variable named by `ContainerDetailsOptions.PodNameVariable` (default `POD_NAME`); in Kubernetes otherwise `HOSTNAME`, which Kubernetes sets to the pod name (but to the node name for `hostNetwork: true` pods, and to `spec.hostname` when that is set: use the Downward API variable for an exact value); else `null` |
| `Namespace` | The environment variable named by `NamespaceVariable` (default `POD_NAMESPACE`); in Kubernetes otherwise the trimmed contents of `/var/run/secrets/kubernetes.io/serviceaccount/namespace`; else `null` |
| `NodeName` | The environment variable named by `NodeNameVariable` (default `NODE_NAME`), else `null`. Kubernetes doesn't set it: expose it through the Downward API (`fieldRef: spec.nodeName`) |

- Environment variable values are trimmed; empty values count as unset.
- Detection never fails the request: a file that can't be read counts as missing. Only the `namespace` file is read, never the service account token next to it.
- cgroup parsing and calls to the Kubernetes API are out of scope: they are fragile and need permissions.

## 9. Security considerations

- `/appinfo` exposes internal details: file paths, host names and infrastructure addresses. **It is open by default**, and the application owner must protect it, for example with `.RequireAuthorization()`, `.RequireHost()` or network policies.
- The core package never emits secrets on its own. Only explicitly enabled packages add potentially sensitive data, and `Dut00.AppInfo.ConnectionStrings` always masks.
- Custom properties (`WithProperty`) are emitted as-is. Their content is the user's responsibility.
- `Urls`, `Container` and `Runtime` reveal ports, pod and node names and exact versions, which helps an attacker map the infrastructure: one more reason to protect the endpoint.
- `Build` reads only the configuration keys and environment variables named in §7, plus any variables the user adds to the `*EnvironmentVariables` lists.
- `Container` reads only the environment variables and files named in §8.
- No caching is applied. Responses reflect the state at request time.
- Values may contain arbitrary text (custom properties, configuration, connection string names), and HTML-sensitive characters such as `<` are not escaped (§4.5). A tool that embeds the body in HTML must encode it like any untrusted text.

## 10. Non-goals

- Health status or liveness semantics. Use ASP.NET Core Health Checks for that.
- An HTML UI. The endpoint is JSON only.
- Writing or changing configuration.
- Querying external systems (the Kubernetes API, cloud metadata endpoints, git) at request time.
- (After v1: CI and a tag-triggered workflow that publishes releases to nuget.org were added; see the README. v0.2 adds the Build, Runtime, Container and Server packages and groups the process fields under `Process`.)
