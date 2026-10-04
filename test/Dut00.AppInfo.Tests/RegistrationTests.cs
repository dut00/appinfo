using Dut00.AppInfo.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void AddAppInfo_CalledTwice_RegistersCoreContributorOnceAndAppliesEveryConfigure()
    {
        var services = new ServiceCollection();

        services.AddAppInfo(o => o.JsonSerializerOptions.WriteIndented = false);
        services.AddAppInfo(o => o.JsonSerializerOptions.MaxDepth = 7);

        ContributorDescriptors(services).Count(d => d.ImplementationType == typeof(CoreAppInfoContributor)).ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(AppInfoMarkerService)).ShouldBe(1);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AppInfoOptions>>().Value;
        options.JsonSerializerOptions.WriteIndented.ShouldBeFalse();
        options.JsonSerializerOptions.MaxDepth.ShouldBe(7);
    }

    [Fact]
    public void WithOwner_CalledTwice_RegistersContributorOnce()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithOwner(o => o.Owner = "first")
            .WithOwner(o => o.Owner = "second");

        ContributorDescriptors(services).Count(d => d.ImplementationType == typeof(OwnerAppInfoContributor)).ShouldBe(1);
        services.BuildServiceProvider().GetRequiredService<IOptions<OwnerOptions>>().Value.Owner.ShouldBe("second");
    }

    [Fact]
    public void WithProperty_CalledTwice_RegistersOneContributorPerCall()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithProperty("A", 1)
            .WithProperty("B", _ => 2);

        ContributorDescriptors(services).Count(d => d.ImplementationInstance is DelegateAppInfoContributor).ShouldBe(2);
    }

    [Fact]
    public void WithContributor_CalledTwice_RegistersScopedContributorOnce()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithContributor<NoOpContributor>()
            .WithContributor<NoOpContributor>();

        var descriptor = ContributorDescriptors(services).Where(d => d.ImplementationType == typeof(NoOpContributor)).ShouldHaveSingleItem();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithProperty_InvalidKey_Throws(string? key)
    {
        var builder = new ServiceCollection().AddAppInfo();

        Should.Throw<ArgumentException>(() => builder.WithProperty(key!, "value"));
        Should.Throw<ArgumentException>(() => builder.WithProperty(key!, _ => "value"));
    }

    [Fact]
    public void WithProperty_DelegateAsConstantValue_Throws()
    {
        var builder = new ServiceCollection().AddAppInfo();

        var exception = Should.Throw<ArgumentException>(() => builder.WithProperty("Now", () => DateTime.UtcNow));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithProperty_PlainNull_BindsToFactoryOverloadAndThrows()
    {
        var builder = new ServiceCollection().AddAppInfo();

        Should.Throw<ArgumentNullException>(() => builder.WithProperty("Key", null!)).ParamName.ShouldBe("factory");
    }

    [Fact]
    public void BuilderMethods_NullArguments_Throw()
    {
        var builder = new ServiceCollection().AddAppInfo();

        Should.Throw<ArgumentNullException>(() => ((IServiceCollection)null!).AddAppInfo());
        Should.Throw<ArgumentNullException>(() => builder.WithOwner(null!));
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithOwner(_ => { }));
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithProperty("Key", "value"));
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithContributor<NoOpContributor>());
    }

    private static IEnumerable<ServiceDescriptor> ContributorDescriptors(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IAppInfoContributor));

    private sealed class NoOpContributor : IAppInfoContributor
    {
        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
