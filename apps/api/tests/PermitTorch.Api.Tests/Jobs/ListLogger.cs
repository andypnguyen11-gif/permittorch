using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PermitTorch.Api.Tests.Jobs;

// Captures formatted log entries so tests can assert operator-facing warnings.
public sealed class ListLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, string Message);

    public ConcurrentQueue<Entry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Enqueue(new Entry(logLevel, formatter(state, exception)));
}
