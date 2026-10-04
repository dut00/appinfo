namespace Dut00.AppInfo.Configuration;

/// <summary>
/// Options for the <c>ConfigurationsFiles</c> field added by
/// <see cref="ConfigurationAppInfoBuilderExtensions.WithConfigurationDetails"/>.
/// </summary>
public sealed class ConfigurationDetailsOptions
{
    /// <summary>
    /// Whether optional configuration files that don't exist are listed too, for example
    /// <c>appsettings.Production.json</c> in an application that has no such file. Default: <see langword="false"/>.
    /// </summary>
    public bool IncludeMissingOptionalFiles { get; set; }
}
