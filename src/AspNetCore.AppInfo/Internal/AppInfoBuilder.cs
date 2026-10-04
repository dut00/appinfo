using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.AppInfo.Internal;

internal sealed class AppInfoBuilder(IServiceCollection services) : IAppInfoBuilder
{
    public IServiceCollection Services { get; } = services;
}
