using System.Text;
using System.Text.Json;
using AspNetCore.AppInfo.Configuration.Internal;
using AspNetCore.AppInfo.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AspNetCore.AppInfo.Configuration.Tests;

public sealed class ConfigurationDetailsTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task ConfigurationsFiles_ListsLoadedFilesAsAbsolutePathsInLoadOrder()
    {
        var first = _directory.WriteFile("first.json", "{}");
        var second = _directory.WriteFile("second.json", "{}");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(second, optional: false);
            b.Configuration.AddJsonFile(first, optional: true);
            b.Services.AddAppInfo().WithConfigurationDetails();
        });

        var json = await app.GetAppInfoAsync();

        // The test output directory has no appsettings.json, so the default optional files are left out.
        Files(json).ShouldBe([second, first]);
        Files(json).ShouldAllBe(path => Path.IsPathRooted(path));
    }

    [Fact]
    public async Task ConfigurationsFiles_ComesAfterCoreFields()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithConfigurationDetails());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "ConfigurationsFiles"]);
        json.GetProperty("ConfigurationsFiles").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task ConfigurationsFiles_RelativePathWithBasePath_IsResolvedToAbsolutePath()
    {
        var expected = _directory.WriteFile("relative.json", "{}");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(new PhysicalFileProvider(_directory.Path), "relative.json", optional: false, reloadOnChange: false);
            b.Services.AddAppInfo().WithConfigurationDetails();
        });

        Files(await app.GetAppInfoAsync()).ShouldBe([expected]);
    }

    [Fact]
    public async Task ConfigurationsFiles_ListsXmlAndIniFiles()
    {
        var xml = _directory.WriteFile("settings.xml", "<settings><Key>value</Key></settings>");
        var ini = _directory.WriteFile("settings.ini", "Key=value");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddXmlFile(xml, optional: false);
            b.Configuration.AddIniFile(ini, optional: false);
            b.Services.AddAppInfo().WithConfigurationDetails();
        });

        Files(await app.GetAppInfoAsync()).ShouldBe([xml, ini]);
    }

    [Fact]
    public async Task MissingOptionalFile_IsLeftOutByDefault()
    {
        var existing = _directory.WriteFile("existing.json", "{}");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(existing, optional: false);
            b.Configuration.AddJsonFile(_directory.Combine("missing.json"), optional: true);
            b.Services.AddAppInfo().WithConfigurationDetails();
        });

        Files(await app.GetAppInfoAsync()).ShouldBe([existing]);
    }

    [Fact]
    public async Task MissingOptionalFile_IsListedWithIncludeMissingOptionalFiles()
    {
        var missing = _directory.Combine("missing.json");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(missing, optional: true);
            b.Services.AddAppInfo().WithConfigurationDetails(o => o.IncludeMissingOptionalFiles = true);
        });

        var files = Files(await app.GetAppInfoAsync());

        files.ShouldContain(missing);
        // The default appsettings.json of the test output directory is missing too, so it is listed now.
        files.ShouldContain(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
    }

    [Fact]
    public void RequiredFileDeletedAfterStartup_IsStillListed()
    {
        var path = _directory.WriteFile("required.json", "{}");
        var configuration = new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
        File.Delete(path);

        ConfigurationAppInfoContributor.GetConfigurationFiles(configuration, includeMissingOptionalFiles: false).ShouldBe([path]);
    }

    [Theory]
    [InlineData("/missing.json")]
    [InlineData("\\missing.json")]
    public void MissingFileWithLeadingSeparator_IsResolvedUnderTheProviderRoot(string configuredPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(new PhysicalFileProvider(_directory.Path), configuredPath, optional: true, reloadOnChange: false)
            .Build();

        var file = ConfigurationAppInfoContributor.GetConfigurationFiles(configuration, includeMissingOptionalFiles: true)
            .ShouldHaveSingleItem();

        // On Windows both separators are stripped; on Linux and macOS "\missing.json" is a legal file name in the root.
        var expected = OperatingSystem.IsWindows() || configuredPath.StartsWith('/') ? "missing.json" : "\\missing.json";
        file.ShouldBe(_directory.Combine(expected));
    }

    [Fact]
    public async Task ChainedConfiguration_FilesInsideItAreListed()
    {
        var outer = _directory.WriteFile("outer.json", "{}");
        var inner = _directory.WriteFile("inner.json", "{}");
        var chained = new ConfigurationBuilder().AddJsonFile(inner, optional: false).Build();
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(outer, optional: false);
            b.Configuration.AddConfiguration(chained);
            b.Services.AddAppInfo().WithConfigurationDetails();
        });

        Files(await app.GetAppInfoAsync()).ShouldBe([outer, inner]);
    }

    [Fact]
    public void ChainedConfiguration_SameRootTwice_IsListedOnce()
    {
        var inner = _directory.WriteFile("inner.json", "{}");
        var chained = new ConfigurationBuilder().AddJsonFile(inner, optional: false).Build();
        var configuration = new ConfigurationBuilder().AddConfiguration(chained).AddConfiguration(chained).Build();

        ConfigurationAppInfoContributor.GetConfigurationFiles(configuration, includeMissingOptionalFiles: false).ShouldBe([inner]);
    }

    [Fact]
    public void NonPhysicalFileProvider_ConfiguredPathIsUsed()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(new InMemoryFileProvider("{}"), "embedded/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        ConfigurationAppInfoContributor.GetConfigurationFiles(configuration, includeMissingOptionalFiles: false)
            .ShouldBe(["embedded/appsettings.json"]);
    }

    [Fact]
    public void ConfigurationThatIsNotARoot_ListsNothing()
    {
        IConfiguration section = new ConfigurationBuilder().Build().GetSection("Any");

        ConfigurationAppInfoContributor.GetConfigurationFiles(section, includeMissingOptionalFiles: true).ShouldBeEmpty();
    }

    [Fact]
    public void WithConfigurationDetails_CalledTwice_RegistersOnceAndAppliesEveryConfigure()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithConfigurationDetails(o => o.IncludeMissingOptionalFiles = false)
            .WithConfigurationDetails(o => o.IncludeMissingOptionalFiles = true);

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationType == typeof(ConfigurationAppInfoContributor))
            .ShouldBe(1);
        services.BuildServiceProvider().GetRequiredService<IOptions<ConfigurationDetailsOptions>>().Value
            .IncludeMissingOptionalFiles.ShouldBeTrue();
    }

    [Fact]
    public void WithConfigurationDetails_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithConfigurationDetails());
    }

    [Fact]
    public async Task KeyNamingPolicy_ConvertsKeyButNotPaths()
    {
        var file = _directory.WriteFile("Settings.json", "{}");
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddJsonFile(file, optional: false);
            b.Services.AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase).WithConfigurationDetails();
        });

        var json = await app.GetAppInfoAsync();

        json.GetProperty("configurationsFiles").EnumerateArray().Select(e => e.GetString()).ShouldBe([file]);
    }

    private static string?[] Files(JsonElement json) =>
        [.. json.GetProperty("ConfigurationsFiles").EnumerateArray().Select(e => e.GetString())];

    /// <summary>
    /// Serves every path from memory, without a physical path, like an embedded resource provider.
    /// </summary>
    private sealed class InMemoryFileProvider(string content) : IFileProvider
    {
        public IFileInfo GetFileInfo(string subpath) => new InMemoryFileInfo(Path.GetFileName(subpath), content);

        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class InMemoryFileInfo(string name, string content) : IFileInfo
    {
        public bool Exists => true;

        public long Length => Encoding.UTF8.GetByteCount(content);

        public string? PhysicalPath => null;

        public string Name => name;

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public bool IsDirectory => false;

        public Stream CreateReadStream() => new MemoryStream(Encoding.UTF8.GetBytes(content));
    }
}
