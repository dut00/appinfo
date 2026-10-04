using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Internal;

internal sealed class AppInfoBuilder(IServiceCollection services) : IAppInfoBuilder
{
    public IServiceCollection Services { get; } = services;
}
