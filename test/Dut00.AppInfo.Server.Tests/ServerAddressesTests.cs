using System.Text.Json;
using Dut00.AppInfo.Server.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Server.Tests;

public sealed class ServerAddressesTests
{
    [Fact]
    public async Task WithServerAddresses_AddsUrlsAfterCoreFields()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithServerAddresses());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Urls"]);
    }

    [Fact]
    public async Task TestServer_HasAnEmptyAddressesFeature()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithServerAddresses());

        var urls = (await app.GetAppInfoAsync()).GetProperty("Urls");

        urls.ValueKind.ShouldBe(JsonValueKind.Array);
        urls.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void ReadAddresses_CopiesTheAddressesAsReported()
    {
        var server = new FakeServer("http://[::]:8080", "https://localhost:8443");

        var addresses = ServerAppInfoContributor.ReadAddresses(server);

        addresses.ShouldNotBeNull();
        addresses.ShouldBe(["http://[::]:8080", "https://localhost:8443"]);
        server.Addresses!.Add("http://later:1");
        addresses.Length.ShouldBe(2); // a copy, not the live collection
    }

    [Fact]
    public void ReadAddresses_WithoutAddressesFeature_IsNull()
    {
        ServerAppInfoContributor.ReadAddresses(new FakeServer(withFeature: false)).ShouldBeNull();
    }

    [Fact]
    public void ReadAddresses_WithoutServer_IsNull()
    {
        ServerAppInfoContributor.ReadAddresses(null).ShouldBeNull();
    }

    [Fact]
    public async Task KeyNamingPolicy_AppliesToTheKey()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithServerAddresses());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldContain("urls");
    }

    [Fact]
    public void WithServerAddresses_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithServerAddresses()
            .WithServerAddresses();

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationFactory is not null).ShouldBe(1);
    }

    [Fact]
    public void WithServerAddresses_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithServerAddresses());
    }

    private sealed class FakeServer : IServer
    {
        public FakeServer(params string[] addresses)
            : this(withFeature: true, addresses)
        {
        }

        public FakeServer(bool withFeature, params string[] addresses)
        {
            if (withFeature)
            {
                var feature = new ServerAddressesFeature();
                foreach (var address in addresses)
                {
                    feature.Addresses.Add(address);
                }

                Features.Set<IServerAddressesFeature>(feature);
            }
        }

        public IFeatureCollection Features { get; } = new FeatureCollection();

        public ICollection<string>? Addresses => Features.Get<IServerAddressesFeature>()?.Addresses;

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
