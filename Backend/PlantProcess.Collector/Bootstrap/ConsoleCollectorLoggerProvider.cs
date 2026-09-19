using System.Globalization;
using Microsoft.Extensions.Logging;

namespace PlantProcess.Collector.Bootstrap;

/// <summary>
/// Dependency-free console logging for the collector executable. It writes the formatted
/// message only, so nothing a caller placed in a scope can leak into the operator console.
/// </summary>
public sealed class ConsoleCollectorLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _minimum;

    public ConsoleCollectorLoggerProvider(LogLevel minimum) => _minimum = minimum;

    public ILogger CreateLogger(string categoryName) => new ConsoleCollectorLogger(categoryName, _minimum);

    public void Dispose()
    {
    }

    private sealed class ConsoleCollectorLogger : ILogger
    {
        private readonly string _category;
        private readonly LogLevel _minimum;

        internal ConsoleCollectorLogger(string category, LogLevel minimum)
        {
            _category = category;
            _minimum = minimum;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimum && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || formatter is null)
            {
                return;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:O} [{1}] {2}: {3}",
                DateTimeOffset.UtcNow,
                logLevel,
                _category,
                formatter(state, exception));

            Console.WriteLine(line);

            if (exception is not null)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}