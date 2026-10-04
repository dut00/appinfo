using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dut00.AppInfo;

/// <summary>
/// The <c>/appinfo</c> response being built for a single request.
/// </summary>
public sealed class AppInfoContext
{
    private readonly List<KeyValuePair<string, object?>> _entries = [];
    private readonly Dictionary<string, int> _indexByKey = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    internal AppInfoContext(HttpContext httpContext, ILogger logger)
    {
        HttpContext = httpContext;
        _logger = logger;
    }

    /// <summary>
    /// The current HTTP request.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// The request's service provider.
    /// </summary>
    public IServiceProvider Services => HttpContext.RequestServices;

    /// <summary>
    /// Keys and values in the order they were first set.
    /// </summary>
    internal IReadOnlyList<KeyValuePair<string, object?>> Entries => _entries;

    /// <summary>
    /// Sets a top-level key in the response.
    /// </summary>
    /// <remarks>
    /// If the key was already set, the new value replaces the old one, the key keeps its original position,
    /// and a warning is logged.
    /// </remarks>
    /// <param name="key">The key, emitted as-is unless a naming policy is configured.</param>
    /// <param name="value">Any JSON-serializable value, or <see langword="null"/>.</param>
    public void Set(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_indexByKey.TryGetValue(key, out var index))
        {
            _logger.LogWarning("AppInfo key '{Key}' was set more than once. The last value wins.", key);
            _entries[index] = new(key, value);
            return;
        }

        _indexByKey[key] = _entries.Count;
        _entries.Add(new(key, value));
    }
}
