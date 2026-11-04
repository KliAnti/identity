using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Identity.Tests.Tests.Helpers;

/// <summary>
/// Сохраняет записи логов в памяти, чтобы тесты могли проверить их содержимое.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// Возвращает записи, созданные всеми логгерами этого провайдера.
    /// </summary>
    public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

    /// <summary>
    /// Создаёт логгер, который сохраняет записи в коллекцию провайдера.
    /// </summary>
    /// <param name="categoryName">Категория логгера.</param>
    /// <returns>Логгер, добавляющий записи в <see cref="Entries"/>.</returns>
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

    /// <summary>
    /// Освобождает ресурсы провайдера логов.
    /// </summary>
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToArray()
                : Array.Empty<KeyValuePair<string, object?>>();

            entries.Enqueue(new CapturedLogEntry(logLevel, formatter(state, exception), properties));
        }
    }
}

internal sealed record CapturedLogEntry(
    LogLevel Level,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> Properties);
