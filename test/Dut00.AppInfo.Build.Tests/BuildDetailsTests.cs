using System.Reflection;
using System.Text.Json;
using Dut00.AppInfo.Build.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Build.Tests;

public sealed class BuildDetailsTests
{
    private const string Sha = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";

    [Fact]
    public async Task WithBuildDetails_AddsBuildAfterCoreFieldsWithPropertiesInOrder()
    {
        var json = await GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Build"]);
        json.GetProperty("Build").PropertyNames().ShouldBe(["InformationalVersion", "Commit", "Branch", "BuildId"]);
    }

    [Fact]
    public async Task NothingFound_WritesAllNulls()
    {
        var build = (await GetAppInfoAsync()).GetProperty("Build");

        foreach (var property in build.EnumerateObject())
        {
            property.Value.ValueKind.ShouldBe(JsonValueKind.Null, property.Name);
        }
    }

    [Fact]
    public async Task InformationalVersion_ComesFromTheEntryAssembly()
    {
        var build = (await GetAppInfoAsync(assembly: Assembly("1.0.5+" + Sha))).GetProperty("Build");

        build.GetProperty("InformationalVersion").GetString().ShouldBe("1.0.5+" + Sha);
    }

    [Fact]
    public async Task Configuration_WinsOverEverything()
    {
        var build = (await GetAppInfoAsync(
            configuration: new() { ["Build:Commit"] = " config-commit ", ["Build:Branch"] = "config-branch", ["Build:BuildId"] = "config-id" },
            assembly: Assembly("1.0.5+" + Sha, ("Commit", "stamped-commit"), ("Branch", "stamped-branch"), ("BuildId", "stamped-id")),
            environment: AllCiVariables())).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe("config-commit"); // trimmed
        build.GetProperty("Branch").GetString().ShouldBe("config-branch");
        build.GetProperty("BuildId").GetString().ShouldBe("config-id");
    }

    [Fact]
    public async Task AssemblyMetadata_WinsOverVersionSuffixAndEnvironmentVariables()
    {
        var build = (await GetAppInfoAsync(
            configuration: new() { ["Build:Commit"] = "   " },
            assembly: Assembly("1.0.5+" + Sha, ("commit", "stamped-commit"), ("Branch", "stamped-branch"), ("BuildId", "stamped-id")),
            environment: AllCiVariables())).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe("stamped-commit"); // blank configuration skipped, key ignores case
        build.GetProperty("Branch").GetString().ShouldBe("stamped-branch");
        build.GetProperty("BuildId").GetString().ShouldBe("stamped-id");
    }

    [Fact]
    public async Task VersionSuffix_WinsOverEnvironmentVariablesForTheCommitOnly()
    {
        var build = (await GetAppInfoAsync(assembly: Assembly("1.0.5+" + Sha), environment: AllCiVariables())).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe(Sha);
        build.GetProperty("Branch").GetString().ShouldBe("env-branch");
        build.GetProperty("BuildId").GetString().ShouldBe("env-id");
    }

    [Fact]
    public async Task EnvironmentVariables_AreCheckedInOrderAndBlankValuesSkipped()
    {
        var build = (await GetAppInfoAsync(environment: new()
        {
            ["GIT_COMMIT"] = " ",
            ["GITHUB_SHA"] = "github-sha",
            ["BUILD_SOURCEVERSION"] = "azure-sha",
            ["CI_COMMIT_REF_NAME"] = " gitlab-branch ",
            ["BUILD_BUILDID"] = "azure-id",
        })).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe("github-sha");
        build.GetProperty("Branch").GetString().ShouldBe("gitlab-branch");
        build.GetProperty("BuildId").GetString().ShouldBe("azure-id");
    }

    [Fact]
    public async Task Options_ChangeTheSectionAndTheVariableLists()
    {
        var build = (await GetAppInfoAsync(
            configuration: new() { ["AppInfo:Build:Commit"] = "custom-section-commit", ["Build:Branch"] = "default-section-branch" },
            environment: new() { ["MY_BUILD"] = "my-id", ["BUILD_ID"] = "default-id" },
            configure: o =>
            {
                o.ConfigurationSection = "AppInfo:Build";
                o.BuildIdEnvironmentVariables.Clear();
                o.BuildIdEnvironmentVariables.Add(" ");
                o.BuildIdEnvironmentVariables.Add("MY_BUILD");
            })).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe("custom-section-commit");
        build.GetProperty("Branch").ValueKind.ShouldBe(JsonValueKind.Null);
        build.GetProperty("BuildId").GetString().ShouldBe("my-id");
    }

    [Theory]
    [InlineData("1.0.5+" + Sha, Sha)]
    [InlineData("1.0.5+a1b2c3d", "a1b2c3d")]
    [InlineData("1.0.5+build.42." + Sha, Sha)]
    [InlineData("1.0.5-preview.1+" + Sha, Sha)]
    [InlineData("1.0+a1b2c3d+e5f6a7b", null)] // after the first +, "a1b2c3d+e5f6a7b" isn't hex
    [InlineData("1.0.5", null)]
    [InlineData("1.0.5+", null)]
    [InlineData("1.0.5+a1b2c3", null)] // 6 digits
    [InlineData("1.0.5+build.42", null)]
    [InlineData("1.0.5+g1b2c3d4", null)]
    [InlineData(null, null)]
    public void ParseCommit_TakesTheHexSegmentTheSdkAppends(string? informationalVersion, string? expected)
    {
        BuildAppInfoContributor.ParseCommit(informationalVersion).ShouldBe(expected);
    }

    [Fact]
    public void ParseCommit_AcceptsSha256AndRejectsLonger()
    {
        var sha256 = new string('a', 64);

        BuildAppInfoContributor.ParseCommit("1.0+" + sha256).ShouldBe(sha256);
        BuildAppInfoContributor.ParseCommit("1.0+" + sha256 + "b").ShouldBeNull();
    }

    [Fact]
    public void BuildAssemblyInfo_Current_ReadsTheEntryAssembly()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly()!;

        BuildAssemblyInfo.Current.InformationalVersion
            .ShouldBe(entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    }

    [Fact]
    public void BuildAssemblyInfo_FromAssembly_WithoutAssembly_IsEmpty()
    {
        var info = BuildAssemblyInfo.FromAssembly(null);

        info.InformationalVersion.ShouldBeNull();
        info.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void BuildAssemblyInfo_Create_TrimsValuesSkipsBlanksAndLetTheLastAttributeWin()
    {
        var info = BuildAssemblyInfo.Create(
            " 1.0.5+a1b2c3d ",
            [("Commit", "first"), ("commit", " second "), ("Branch", " "), ("BuildId", null)]);

        info.InformationalVersion.ShouldBe("1.0.5+a1b2c3d");
        info.Metadata["COMMIT"].ShouldBe("second");
        info.Metadata.ContainsKey("Branch").ShouldBeFalse();
        info.Metadata.ContainsKey("BuildId").ShouldBeFalse();
    }

    [Fact]
    public async Task BlankAssemblyMetadata_FallsThroughToTheVersionSuffix()
    {
        var build = (await GetAppInfoAsync(
            assembly: BuildAssemblyInfo.Create("1.0.5+" + Sha, [("Commit", "  ")]),
            environment: AllCiVariables())).GetProperty("Build");

        build.GetProperty("Commit").GetString().ShouldBe(Sha);
    }

    [Fact]
    public void BuildAssemblyInfo_Create_BlankVersion_IsNull()
    {
        BuildAssemblyInfo.Create("  ", []).InformationalVersion.ShouldBeNull();
    }

    [Fact]
    public async Task KeyNamingPolicy_AppliesToNestedNames()
    {
        var json = await GetAppInfoAsync(configureAppInfo: o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase);

        json.GetProperty("build").PropertyNames().ShouldBe(["informationalVersion", "commit", "branch", "buildId"]);
    }

    [Fact]
    public void WithBuildDetails_CalledTwice_RegistersOnceAndAppliesEveryConfigure()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithBuildDetails(o => o.ConfigurationSection = "First")
            .WithBuildDetails(o => o.CommitEnvironmentVariables.Add("SECOND"));

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationFactory is not null).ShouldBe(1);
        var options = services.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Options.IOptions<BuildDetailsOptions>>().Value;
        options.ConfigurationSection.ShouldBe("First");
        options.CommitEnvironmentVariables.ShouldContain("SECOND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ConfigurationSection_Blank_Throws(string? section)
    {
        Should.Throw<ArgumentException>(() => new BuildDetailsOptions().ConfigurationSection = section!);
    }

    [Fact]
    public void WithBuildDetails_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithBuildDetails());
    }

    private static Dictionary<string, string?> AllCiVariables() => new()
    {
        ["GITHUB_SHA"] = "env-sha",
        ["GITHUB_REF_NAME"] = "env-branch",
        ["GITHUB_RUN_ID"] = "env-id",
    };

    private static BuildAssemblyInfo Assembly(string? informationalVersion, params (string Key, string Value)[] metadata) =>
        new(informationalVersion, metadata.ToDictionary(m => m.Key, m => m.Value, StringComparer.OrdinalIgnoreCase));

    // Always replaces the entry assembly and the environment variables: the test project is the entry assembly,
    // and CI sets the very variables this package reads.
    private static async Task<JsonElement> GetAppInfoAsync(
        Dictionary<string, string?>? configuration = null,
        BuildAssemblyInfo? assembly = null,
        Dictionary<string, string?>? environment = null,
        Action<BuildDetailsOptions>? configure = null,
        Action<AppInfoOptions>? configureAppInfo = null)
    {
        var variables = environment ?? [];
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddInMemoryCollection(configuration ?? []);
            b.Services.AddSingleton(assembly ?? Assembly(null));
            b.Services.AddSingleton(new EnvironmentVariables(name => variables.GetValueOrDefault(name)));
            b.Services.AddAppInfo(configureAppInfo).WithBuildDetails(configure);
        });

        return await app.GetAppInfoAsync();
    }
}
