using System.Text.Json;
using Dut00.AppInfo.Container.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Container.Tests;

public sealed class ContainerDetailsTests
{
    private const string Pod = "billing-api-7f9c6d5b4-x2kqp";

    [Fact]
    public async Task WithContainerDetails_AddsContainerAfterCoreFieldsWithPropertiesInOrder()
    {
        var json = await GetAppInfoAsync(new FakeHost());

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Container"]);
        json.GetProperty("Container").PropertyNames().ShouldBe(["IsContainer", "IsKubernetes", "PodName", "Namespace", "NodeName"]);
    }

    [Fact]
    public async Task NoContainer_IsFalseAndNull()
    {
        var container = (await GetAppInfoAsync(new FakeHost())).GetProperty("Container");

        container.GetProperty("IsContainer").GetBoolean().ShouldBeFalse();
        container.GetProperty("IsKubernetes").GetBoolean().ShouldBeFalse();
        container.GetProperty("PodName").ValueKind.ShouldBe(JsonValueKind.Null);
        container.GetProperty("Namespace").ValueKind.ShouldBe(JsonValueKind.Null);
        container.GetProperty("NodeName").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(ContainerAppInfoContributor.DockerEnvFile)]
    [InlineData(ContainerAppInfoContributor.PodmanEnvFile)]
    public void MarkerFile_OnLinux_IsAContainerButNotKubernetes(string file)
    {
        var details = Detect(new FakeHost { Files = { [file] = "" } });

        details.ShouldBe(new ContainerDetails(IsContainer: true, IsKubernetes: false, PodName: null, Namespace: null, NodeName: null));
    }

    [Fact]
    public void MarkerFile_OnWindows_IsIgnored()
    {
        var details = Detect(new FakeHost { IsWindows = true, Files = { [ContainerAppInfoContributor.DockerEnvFile] = "" } });

        details.IsContainer.ShouldBeFalse();
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("true", false, true)]
    [InlineData(" TRUE ", true, true)]
    [InlineData(" TRUE ", false, true)]
    [InlineData("false", false, false)]
    [InlineData("1", false, false)]
    [InlineData("", true, false)]
    public void OfficialDotNetImageVariable_MeansContainerOnAnyOs(string value, bool isWindows, bool expected)
    {
        var details = Detect(new FakeHost { IsWindows = isWindows, Variables = { ["DOTNET_RUNNING_IN_CONTAINER"] = value } });

        details.IsContainer.ShouldBe(expected);
        details.IsKubernetes.ShouldBeFalse();
    }

    [Fact]
    public void Kubernetes_WithoutDownwardApi_UsesHostNameAndTheNamespaceFile()
    {
        var details = Detect(new FakeHost
        {
            Variables = { ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1", ["HOSTNAME"] = Pod },
            Files = { [ContainerAppInfoContributor.NamespaceFile] = "stage\n" },
        });

        details.ShouldBe(new ContainerDetails(IsContainer: true, IsKubernetes: true, PodName: Pod, Namespace: "stage", NodeName: null));
    }

    [Fact]
    public void Kubernetes_WithDownwardApi_PrefersTheVariables()
    {
        var details = Detect(new FakeHost
        {
            Variables =
            {
                ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1",
                ["HOSTNAME"] = "custom-hostname",
                ["POD_NAME"] = $" {Pod} ",
                ["POD_NAMESPACE"] = "prod",
                ["NODE_NAME"] = "aks-node-3",
            },
            Files = { [ContainerAppInfoContributor.NamespaceFile] = "stage" },
        });

        details.ShouldBe(new ContainerDetails(IsContainer: true, IsKubernetes: true, PodName: Pod, Namespace: "prod", NodeName: "aks-node-3"));
    }

    [Fact]
    public void OutsideKubernetes_IgnoresHostNameAndTheNamespaceFile()
    {
        var details = Detect(new FakeHost
        {
            Variables = { ["HOSTNAME"] = "laptop", ["KUBERNETES_SERVICE_HOST"] = "  " },
            Files = { [ContainerAppInfoContributor.NamespaceFile] = "stage" },
        });

        details.ShouldBe(new ContainerDetails(IsContainer: false, IsKubernetes: false, PodName: null, Namespace: null, NodeName: null));
    }

    [Fact]
    public void OutsideKubernetes_StillReportsDownwardApiStyleVariables()
    {
        var details = Detect(new FakeHost { Variables = { ["POD_NAME"] = "worker-1", ["NODE_NAME"] = "host-7" } });

        details.PodName.ShouldBe("worker-1");
        details.NodeName.ShouldBe("host-7");
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(NotSupportedException))]
    public void UnreadableNamespaceFile_CountsAsMissing(Type exceptionType)
    {
        var host = new FakeHost { Variables = { ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1" } };
        host.Failures[ContainerAppInfoContributor.NamespaceFile] = (Exception)Activator.CreateInstance(exceptionType)!;

        Detect(host).Namespace.ShouldBeNull();
    }

    [Fact]
    public void BlankNamespaceFile_IsNull()
    {
        var details = Detect(new FakeHost
        {
            Variables = { ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1" },
            Files = { [ContainerAppInfoContributor.NamespaceFile] = " \n" },
        });

        details.Namespace.ShouldBeNull();
    }

    [Fact]
    public void OnlyTheNamespaceFileIsRead()
    {
        var host = new FakeHost
        {
            Variables = { ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1" },
            Files = { [ContainerAppInfoContributor.NamespaceFile] = "stage" },
        };

        Detect(host);

        host.ReadFiles.ShouldBe([ContainerAppInfoContributor.NamespaceFile]);
    }

    [Fact]
    public void Options_ChangeTheVariableNames()
    {
        var options = new ContainerDetailsOptions { PodNameVariable = "MY_POD", NamespaceVariable = "MY_NS", NodeNameVariable = "MY_NODE" };

        var details = ContainerAppInfoContributor.Detect(options, new FakeHost
        {
            Variables = { ["MY_POD"] = "p", ["MY_NS"] = "n", ["MY_NODE"] = "x", ["POD_NAME"] = "ignored" },
        }.ToHost());

        details.ShouldBe(new ContainerDetails(IsContainer: false, IsKubernetes: false, PodName: "p", Namespace: "n", NodeName: "x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Options_BlankVariableName_Throws(string? name)
    {
        var options = new ContainerDetailsOptions();

        Should.Throw<ArgumentException>(() => options.PodNameVariable = name!);
        Should.Throw<ArgumentException>(() => options.NamespaceVariable = name!);
        Should.Throw<ArgumentException>(() => options.NodeNameVariable = name!);
    }

    [Fact]
    public async Task Detection_RunsOnceOnTheFirstRequest()
    {
        var host = new FakeHost { Variables = { ["KUBERNETES_SERVICE_HOST"] = "10.0.0.1" } };
        await using var app = await StartAsync(host);

        host.VariableReads.ShouldBe(0);
        await app.GetAppInfoAsync();
        var readsAfterFirstRequest = host.VariableReads;
        await app.GetAppInfoAsync();

        readsAfterFirstRequest.ShouldBeGreaterThan(0);
        host.VariableReads.ShouldBe(readsAfterFirstRequest);
    }

    [Fact]
    public async Task KeyNamingPolicy_AppliesToNestedNames()
    {
        await using var app = await StartAsync(new FakeHost(), configureAppInfo: o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase);

        var json = await app.GetAppInfoAsync();

        json.GetProperty("container").PropertyNames().ShouldBe(["isContainer", "isKubernetes", "podName", "namespace", "nodeName"]);
    }

    [Fact]
    public void WithContainerDetails_CalledTwice_RegistersOnceAndAppliesEveryConfigure()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithContainerDetails(o => o.PodNameVariable = "FIRST")
            .WithContainerDetails(o => o.NodeNameVariable = "SECOND");

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationFactory is not null).ShouldBe(1);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ContainerDetailsOptions>>().Value;
        options.PodNameVariable.ShouldBe("FIRST");
        options.NodeNameVariable.ShouldBe("SECOND");
    }

    [Fact]
    public void WithContainerDetails_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithContainerDetails());
    }

    [Fact]
    public void ContainerHost_Current_DetectsWithoutThrowing()
    {
        // Whatever the machine is, real detection must not fail.
        Should.NotThrow(() => ContainerAppInfoContributor.Detect(new ContainerDetailsOptions(), ContainerHost.Current));
    }

    private static ContainerDetails Detect(FakeHost host) =>
        ContainerAppInfoContributor.Detect(new ContainerDetailsOptions(), host.ToHost());

    private static Task<TestApp> StartAsync(FakeHost host, Action<AppInfoOptions>? configureAppInfo = null) =>
        TestApp.StartAsync(b =>
        {
            b.Services.AddSingleton(host.ToHost());
            b.Services.AddAppInfo(configureAppInfo).WithContainerDetails();
        });

    private static async Task<JsonElement> GetAppInfoAsync(FakeHost host)
    {
        await using var app = await StartAsync(host);
        return await app.GetAppInfoAsync();
    }

    // Fake environment variables and files; nothing touches the real machine.
    private sealed class FakeHost
    {
        public Dictionary<string, string> Variables { get; } = [];

        public Dictionary<string, string> Files { get; } = [];

        public Dictionary<string, Exception> Failures { get; } = [];

        public bool IsWindows { get; init; }

        public int VariableReads { get; private set; }

        public List<string> ReadFiles { get; } = [];

        public ContainerHost ToHost() => new(
            name =>
            {
                VariableReads++;
                return Variables.GetValueOrDefault(name);
            },
            path => Files.ContainsKey(path) || Failures.ContainsKey(path),
            path =>
            {
                ReadFiles.Add(path);
                return Failures.TryGetValue(path, out var failure) ? throw failure
                    : Files.TryGetValue(path, out var content) ? content
                    : throw new FileNotFoundException(path);
            },
            IsWindows);
    }
}
