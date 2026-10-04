using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspNetCore.AppInfo.Tests;

public sealed class AppInfoContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Set_InvalidKey_Throws(string? key)
    {
        var context = CreateContext();

        Should.Throw<ArgumentException>(() => context.Set(key!, "value"));
    }

    [Fact]
    public void Set_KeysAreCaseSensitive()
    {
        var context = CreateContext();

        context.Set("Key", 1);
        context.Set("key", 2);

        context.Entries.Select(e => e.Key).ShouldBe(["Key", "key"]);
    }

    [Fact]
    public void Set_SameKeyTwice_ReplacesValueInPlace()
    {
        var context = CreateContext();

        context.Set("A", 1);
        context.Set("B", 2);
        context.Set("A", 3);

        context.Entries.ShouldBe([new("A", 3), new("B", 2)]);
    }

    [Fact]
    public void Services_IsTheRequestServiceProvider()
    {
        using var requestServices = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = requestServices };
        var context = new AppInfoContext(httpContext, NullLogger.Instance);

        context.HttpContext.ShouldBeSameAs(httpContext);
        context.Services.ShouldBeSameAs(requestServices);
    }

    private static AppInfoContext CreateContext() => new(new DefaultHttpContext(), NullLogger.Instance);
}
