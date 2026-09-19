using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Collector.Tests.TestSupport;

/// <summary>Captures every log line the collector and the stack emit during a test.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    internal IReadOnlyList<string> Lines => _lines.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<string> _lines;

        internal CapturingLogger(string category, ConcurrentQueue<string> lines)
        {
            _category = category;
            _lines = lines;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter is null ? state?.ToString() ?? string.Empty : formatter(state, exception);
            _lines.Enqueue(_category + " | " + message + " | " + exception);
        }
    }
}