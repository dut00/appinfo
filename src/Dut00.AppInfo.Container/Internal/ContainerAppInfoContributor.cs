using System.Security;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Container.Internal;

/// <summary>
/// Writes the <c>Container</c> group. Detection runs once, on the first request.
/// </summary>
internal sealed class ContainerAppInfoContributor : IAppInfoContributor
{
    internal const string DockerEnvFile = "/.dockerenv";
    internal const string PodmanEnvFile = "/run/.containerenv";
    internal const string NamespaceFile = "/var/run/secrets/kubernetes.io/serviceaccount/namespace";

    private readonly Lazy<ContainerDetails> _details;

    public ContainerAppInfoContributor(IOptions<ContainerDetailsOptions> options, ContainerHost host)
    {
        _details = new(() => Detect(options.Value, host));
    }

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Container", _details.Value);
        return ValueTask.CompletedTask;
    }

    internal static ContainerDetails Detect(ContainerDetailsOptions options, ContainerHost host)
    {
        string? Variable(string name) => Normalize(host.GetEnvironmentVariable(name));

        var isKubernetes = Variable("KUBERNETES_SERVICE_HOST") is not null;
        var isContainer = isKubernetes
            || string.Equals(Variable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase)
            || (!host.IsWindows && (host.FileExists(DockerEnvFile) || host.FileExists(PodmanEnvFile)));

        return new ContainerDetails(
            IsContainer: isContainer,
            IsKubernetes: isKubernetes,
            PodName: Variable(options.PodNameVariable) ?? (isKubernetes ? Variable("HOSTNAME") : null),
            Namespace: Variable(options.NamespaceVariable) ?? (isKubernetes ? ReadNamespaceFile(host) : null),
            NodeName: Variable(options.NodeNameVariable));
    }

    // Only this file is read, never the service account token next to it. A file that can't be read counts as missing.
    private static string? ReadNamespaceFile(ContainerHost host)
    {
        try
        {
            return Normalize(host.ReadFile(NamespaceFile));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
