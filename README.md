# AspNetCore.AppInfo

A family of NuGet packages that adds an `/appinfo` endpoint to ASP.NET Core applications.
The endpoint returns JSON describing the running application: name, version, environment, owner, and more through extension packages.

> Work in progress. See [docs/SPEC.md](docs/SPEC.md) for the specification and [docs/PLAN.md](docs/PLAN.md) for the implementation plan.

## Packages

| Package | Description |
| --- | --- |
| `AspNetCore.AppInfo` | Core: endpoint, application name, version, environment, owner, custom properties |
| `AspNetCore.AppInfo.Configuration` | Lists loaded configuration files |
| `AspNetCore.AppInfo.Environment` | Process uptime, host name, content root, assembly location |
| `AspNetCore.AppInfo.ConnectionStrings` | Connection strings with secrets masked |

## License

[MIT](LICENSE)
