using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Features.Authentication.LoginAttemptRateLimiting;

/// <summary>
/// Ограничивает частоту попыток входа по нормализованному адресу электронной почты.
/// </summary>
/// <remarks>
/// Состояние хранится только в памяти текущего процесса: после перезапуска лимиты сбрасываются,
/// а несколько экземпляров сервиса не синхронизируют счётчики.
/// </remarks>
public sealed class LoginAttemptRateLimiter
{
    private const int BucketCount = 65_536;
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly byte[] _partitionKey = RandomNumberGenerator.GetBytes(32);

    private readonly AttemptBucket[] _buckets =
        Enumerable.Range(0, BucketCount)
            .Select(_ => new AttemptBucket(PermitLimit, Window))
            .ToArray();

    /// <summary>
    /// Регистрирует попытку входа и сообщает, разрешена ли она текущим лимитом.
    /// </summary>
    /// <param name="normalizedEmail">Нормализованный адрес; одинаковое значение должно использоваться для одного аккаунта.</param>
    /// <returns>Разрешение на попытку и время ожидания до следующего окна при отказе.</returns>
    public RateLimitResult TryAcquire(string normalizedEmail)
    {
        var digest = HMACSHA256.HashData(_partitionKey, Encoding.UTF8.GetBytes(normalizedEmail));
        var bucketIndex = (int)(BinaryPrimitives.ReadUInt32LittleEndian(digest) % BucketCount);
        return _buckets[bucketIndex].TryAcquire();
    }
}