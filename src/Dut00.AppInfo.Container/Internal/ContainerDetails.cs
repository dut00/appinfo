namespace Dut00.AppInfo.Container.Internal;

/// <summary>
/// The <c>Container</c> group. Properties are serialized in declaration order.
/// </summary>
/// <param name="IsContainer">Whether the app runs in a container (including a Kubernetes pod).</param>
/// <param name="IsKubernetes">Whether the app runs in a Kubernetes pod.</param>
/// <param name="PodName">The pod name, or <see langword="null"/>.</param>
/// <param name="Namespace">The Kubernetes namespace, or <see langword="null"/>.</param>
/// <param name="NodeName">The Kubernetes node, or <see langword="null"/>.</param>
internal sealed record ContainerDetails(bool IsContainer, bool IsKubernetes, string? PodName, string? Namespace, string? NodeName);
