namespace Dut00.AppInfo.Build;

/// <summary>
/// Options for the <c>Build</c> field added by <see cref="BuildAppInfoBuilderExtensions.WithBuildDetails"/>.
/// </summary>
public sealed class BuildDetailsOptions
{
    private string _configurationSection = "Build";

    /// <summary>
    /// The configuration section whose <c>Commit</c>, <c>Branch</c> and <c>BuildId</c> keys override every other source.
    /// Default: <c>Build</c>, so the environment variable <c>Build__Commit</c> sets the commit.
    /// </summary>
    /// <remarks>Only these three keys are read, never the whole section.</remarks>
    /// <exception cref="ArgumentException">The value is <see langword="null"/>, empty or whitespace.</exception>
    public string ConfigurationSection
    {
        get => _configurationSection;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _configurationSection = value;
        }
    }

    /// <summary>
    /// Environment variables checked in order for the commit when no other source has it.
    /// Defaults: <c>GIT_COMMIT</c>, <c>GITHUB_SHA</c>, <c>CI_COMMIT_SHA</c>, <c>BUILD_SOURCEVERSION</c>.
    /// </summary>
    /// <remarks>
    /// CI systems set these while the build runs, not on the host that runs the app, so they help only when the
    /// deployment passes them through. Blank entries are ignored.
    /// </remarks>
    public IList<string> CommitEnvironmentVariables { get; } =
        new List<string> { "GIT_COMMIT", "GITHUB_SHA", "CI_COMMIT_SHA", "BUILD_SOURCEVERSION" };

    /// <summary>
    /// Environment variables checked in order for the branch when no other source has it.
    /// Defaults: <c>GIT_BRANCH</c>, <c>GITHUB_REF_NAME</c>, <c>CI_COMMIT_REF_NAME</c>, <c>BUILD_SOURCEBRANCHNAME</c>.
    /// </summary>
    /// <remarks>Blank entries are ignored.</remarks>
    public IList<string> BranchEnvironmentVariables { get; } =
        new List<string> { "GIT_BRANCH", "GITHUB_REF_NAME", "CI_COMMIT_REF_NAME", "BUILD_SOURCEBRANCHNAME" };

    /// <summary>
    /// Environment variables checked in order for the build ID when no other source has it.
    /// Defaults: <c>BUILD_ID</c>, <c>GITHUB_RUN_ID</c>, <c>CI_PIPELINE_ID</c>, <c>BUILD_BUILDID</c>.
    /// </summary>
    /// <remarks>Blank entries are ignored.</remarks>
    public IList<string> BuildIdEnvironmentVariables { get; } =
        new List<string> { "BUILD_ID", "GITHUB_RUN_ID", "CI_PIPELINE_ID", "BUILD_BUILDID" };
}
