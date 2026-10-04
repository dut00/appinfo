using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AspNetCore.AppInfo.Tests.Infrastructure;

/// <summary>
/// Collects log entries in memory so tests can assert on them.
/// </summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
    }
}

internal sealed record LogEntry(string Category, LogLevel Level, string Message);
