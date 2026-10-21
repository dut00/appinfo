# Dut00.AppInfo.Container

Extends the [Dut00.AppInfo](https://www.nuget.org/packages/Dut00.AppInfo) `/appinfo` endpoint with container and Kubernetes details: whether the app runs in a container or a pod, and which pod, namespace and node.

```csharp
using Dut00.AppInfo;
using Dut00.AppInfo.Container;

builder.Services.AddAppInfo()
    .WithContainerDetails();

var app = builder.Build();
app.MapAppInfo();
```

Adds this key:

```json
{
  "Container": {
    "IsContainer": true,
    "IsKubernetes": true,
    "PodName": "billing-api-7f9c6d5b4-x2kqp",
    "Namespace": "stage",
    "NodeName": "aks-node-3"
  }
}
```

## How it's detected

| Value | Rule |
| --- | --- |
| `IsKubernetes` | `KUBERNETES_SERVICE_HOST` is set and non-empty; Kubernetes injects it into every pod |
| `IsContainer` | In Kubernetes; or `DOTNET_RUNNING_IN_CONTAINER=true`, set by the official .NET images (Linux and Windows); or, on non-Windows systems, `/.dockerenv` (Docker) or `/run/.containerenv` (Podman) exists |
| `PodName` | `POD_NAME`; in Kubernetes otherwise `HOSTNAME` |
| `Namespace` | `POD_NAMESPACE`; in Kubernetes otherwise the service account's `namespace` file |
| `NodeName` | `NODE_NAME`, else `null` |

- Values are read once, on the first request. Detection never fails the request: a file that can't be read counts as missing.
- Only the `namespace` file of the service account is read, never the token next to it. The Kubernetes API is never called.
- `HOSTNAME` is the pod name in most pods, but the node name with `hostNetwork: true` and `spec.hostname` when that is set. For exact values, use the Downward API.

## Kubernetes: exact values with the Downward API

Kubernetes doesn't put the node name (or, in every pod, the exact pod name) into the environment by itself. Add this to the container spec:

```yaml
env:
  - name: POD_NAME
    valueFrom:
      fieldRef:
        fieldPath: metadata.name
  - name: POD_NAMESPACE
    valueFrom:
      fieldRef:
        fieldPath: metadata.namespace
  - name: NODE_NAME
    valueFrom:
      fieldRef:
        fieldPath: spec.nodeName
```

If your manifests already use other names, point the options at them:

```csharp
builder.Services.AddAppInfo()
    .WithContainerDetails(o =>
    {
        o.PodNameVariable = "MY_POD_NAME";
        o.NamespaceVariable = "MY_POD_NAMESPACE";
        o.NodeNameVariable = "MY_NODE_NAME";
    });
```

> Pod, namespace and node names describe your infrastructure. Protect the endpoint, for example with `app.MapAppInfo().RequireAuthorization()`.
