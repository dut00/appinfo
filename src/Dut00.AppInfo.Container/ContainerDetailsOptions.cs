namespace Dut00.AppInfo.Container;

/// <summary>
/// Options for the <c>Container</c> field added by <see cref="ContainerAppInfoBuilderExtensions.WithContainerDetails"/>.
/// </summary>
/// <remarks>
/// Kubernetes doesn't set these variables by itself. Expose them through the Downward API, for example:
/// <code>
/// env:
///   - name: POD_NAME
///     valueFrom: { fieldRef: { fieldPath: metadata.name } }
///   - name: POD_NAMESPACE
///     valueFrom: { fieldRef: { fieldPath: metadata.namespace } }
///   - name: NODE_NAME
///     valueFrom: { fieldRef: { fieldPath: spec.nodeName } }
/// </code>
/// </remarks>
public sealed class ContainerDetailsOptions
{
    private string _podNameVariable = "POD_NAME";
    private string _namespaceVariable = "POD_NAMESPACE";
    private string _nodeNameVariable = "NODE_NAME";

    /// <summary>
    /// The environment variable that holds the pod name. Default: <c>POD_NAME</c>. Without it, the pod name in
    /// Kubernetes falls back to <c>HOSTNAME</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or whitespace.</exception>
    public string PodNameVariable
    {
        get => _podNameVariable;
        set => _podNameVariable = Validate(value);
    }

    /// <summary>
    /// The environment variable that holds the namespace. Default: <c>POD_NAMESPACE</c>. Without it, the namespace in
    /// Kubernetes falls back to the service account's <c>namespace</c> file.
    /// </summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or whitespace.</exception>
    public string NamespaceVariable
    {
        get => _namespaceVariable;
        set => _namespaceVariable = Validate(value);
    }

    /// <summary>
    /// The environment variable that holds the node name. Default: <c>NODE_NAME</c>. There is no fallback.
    /// </summary>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or whitespace.</exception>
    public string NodeNameVariable
    {
        get => _nodeNameVariable;
        set => _nodeNameVariable = Validate(value);
    }

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}
