using System.Text.Json;

namespace AspNetCore.AppInfo.Tests.Infrastructure;

internal static class JsonElementExtensions
{
    /// <summary>
    /// The object's property names in document order.
    /// </summary>
    public static string[] PropertyNames(this JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];
}
