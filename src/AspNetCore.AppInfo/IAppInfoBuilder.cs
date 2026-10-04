using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.AppInfo;

/// <summary>
/// Configures what the <c>/appinfo</c> endpoint returns. Extension packages add <c>With*</c> methods to it.
/// </summary>
public interface IAppInfoBuilder
{
    /// <summary>
    /// The application's service collection.
    /// </summary>
    IServiceCollection Services { get; }
}
