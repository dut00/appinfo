# Dut00.AppInfo.Build

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with the build that produced the running binary: informational version, git commit, branch and CI build ID.

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Build;

builder.Services.AddAppInfo()
    .WithBuildDetails();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key:

```json
{
  "Build": {
    "InformationalVersion": "1.0.5+a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
    "Commit": "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678",
    "Branch": "main",
    "BuildId": "1234"
  }
}
```

## Zero config: what you get out of the box

No configuration is required.

| Value | Without any setup |
| --- | --- |
| `InformationalVersion` | Always there: the SDK writes it from `<Version>` (or `1.0.0`). |
| `Commit` | There when the app is built inside a git repository: the SDK appends the commit to the informational version. |
| `Branch`, `BuildId` | `null` until you stamp them into the binary (recommended, see [Stamping](#stamping-the-build-into-the-binary)), pass CI variables to the running app, or set them in configuration. |

Configuration is only an override with the highest priority, for example for a deployment that knows better than the binary.

## Where the values come from

Each value comes from the first source that has it. Missing values are `null`.

| Value | 1. Configuration | 2. Entry assembly | 3. Fallback | 4. Environment variables |
| --- | --- | --- | --- | --- |
| `InformationalVersion` | | `AssemblyInformationalVersion` | | |
| `Commit` | `Build:Commit` | `[AssemblyMetadata("Commit", ...)]` | The git commit the SDK appends to the informational version (`1.0.5+<sha>`) | `GIT_COMMIT`, `GITHUB_SHA`, `CI_COMMIT_SHA`, `BUILD_SOURCEVERSION` |
| `Branch` | `Build:Branch` | `[AssemblyMetadata("Branch", ...)]` | | `GIT_BRANCH`, `GITHUB_REF_NAME`, `CI_COMMIT_REF_NAME`, `BUILD_SOURCEBRANCHNAME` |
| `BuildId` | `Build:BuildId` | `[AssemblyMetadata("BuildId", ...)]` | | `BUILD_ID`, `GITHUB_RUN_ID`, `CI_PIPELINE_ID`, `BUILD_BUILDID` |

- The .NET SDK appends the git commit to the informational version when it builds inside a git repository, so the commit usually needs no setup.
- **CI variables such as `GITHUB_SHA` exist while the CI job runs, not on the server that runs your app.** Stamp the values into the binary instead; see below.
- Configuration comes first, so a deployment can override what the binary says, for example with the environment variable `Build__Commit`.
- Only these keys and variables are read, never a whole configuration section.

## Stamping the build into the binary

Add `AssemblyMetadata` items to the application's project file. MSBuild reads CI variables as properties.

GitHub Actions:

```xml
<ItemGroup Condition="'$(GITHUB_ACTIONS)' == 'true'">
  <AssemblyMetadata Include="Branch" Value="$(GITHUB_REF_NAME)" />
  <AssemblyMetadata Include="BuildId" Value="$(GITHUB_RUN_ID)" />
</ItemGroup>
```

Azure DevOps:

```xml
<ItemGroup Condition="'$(TF_BUILD)' == 'true'">
  <AssemblyMetadata Include="Branch" Value="$(BUILD_SOURCEBRANCH)" />
  <AssemblyMetadata Include="BuildId" Value="$(BUILD_BUILDID)" />
</ItemGroup>
```

`BUILD_SOURCEBRANCH` is the full ref (`refs/heads/feature/foo`); `BUILD_SOURCEBRANCHNAME`, the default environment variable fallback, keeps only the last segment (`foo`).

GitLab CI:

```xml
<ItemGroup Condition="'$(GITLAB_CI)' == 'true'">
  <AssemblyMetadata Include="Branch" Value="$(CI_COMMIT_REF_NAME)" />
  <AssemblyMetadata Include="BuildId" Value="$(CI_PIPELINE_ID)" />
</ItemGroup>
```

If a Docker build has no `.git` folder, stamp the commit too, for example `<AssemblyMetadata Include="Commit" Value="$(GIT_COMMIT)" />` with `--build-arg GIT_COMMIT=...` passed on as an environment variable.

## Options

```csharp
builder.Services.AddAppInfo()
    .WithBuildDetails(o =>
    {
        o.ConfigurationSection = "AppInfo:Build";          // default: Build
        o.BuildIdEnvironmentVariables.Add("MY_BUILD_ID");  // checked after the defaults
    });
```

`CommitEnvironmentVariables`, `BranchEnvironmentVariables` and `BuildIdEnvironmentVariables` are checked in order; clear them to turn the fallback off.
