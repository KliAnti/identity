using System.Diagnostics;

namespace Identity.Features.Authentication.LoginAttemptRateLimiting;

/// <summary>
/// Хранит счётчик попыток для одной группы запросов в скользящем временном окне.
/// </summary>
/// <remarks>Тип используется только внутри <see cref="LoginAttemptRateLimiter"/> и синхронизирует доступ к счётчику блокировкой.</remarks>
internal sealed class AttemptBucket
{
    private readonly object _sync = new();
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private long _windowStartedAt = Stopwatch.GetTimestamp();
    private int _attempts;

    /// <summary>
    /// Создаёт счётчик с заданным числом разрешённых попыток и длительностью окна.
    /// </summary>
    /// <param name="permitLimit">Максимальное число разрешённых попыток за одно окно; должно быть больше нуля.</param>
    /// <param name="window">Длительность окна; должна быть положительной.</param>
    public AttemptBucket(int permitLimit, TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        _permitLimit = permitLimit;
        _window = window;
    }

    /// <summary>
    /// Атомарно учитывает одну попытку и проверяет, не исчерпан ли лимит.
    /// </summary>
    /// <returns>Разрешён ли запрос; при отказе также возвращается время до начала нового окна.</returns>
    public RateLimitResult TryAcquire()
    {
        lock (_sync)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(_windowStartedAt, now);
            if (elapsed >= _window)
            {
                _windowStartedAt = now;
                _attempts = 0;
                elapsed = TimeSpan.Zero;
            }

            if (_attempts >= _permitLimit)
            {
                return new RateLimitResult(false, _window - elapsed);
            }

            _attempts++;
            return new RateLimitResult(true, TimeSpan.Zero);
        }
    }
}