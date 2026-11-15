namespace Identity.Features.Authentication.LoginAttemptRateLimiting;

/// <summary>
/// Результат проверки лимита попыток входа.
/// </summary>
/// <param name="IsAllowed">Показывает, разрешена ли текущая попытка.</param>
/// <param name="RetryAfter">Время до сброса текущего окна; для разрешённой попытки равно нулю.</param>
public readonly record struct RateLimitResult(bool IsAllowed, TimeSpan RetryAfter);