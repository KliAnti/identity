using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Features.Authentication;

public sealed class LoginAttemptRateLimiter
{
    private const int BucketCount = 65_536;
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly byte[] _partitionKey = RandomNumberGenerator.GetBytes(32);

    private readonly AttemptBucket[] _buckets =
        Enumerable.Range(0, BucketCount).Select(_ => new AttemptBucket()).ToArray();

    public RateLimitResult TryAcquire(string normalizedEmail)
    {
        var digest = HMACSHA256.HashData(_partitionKey, Encoding.UTF8.GetBytes(normalizedEmail));
        var bucketIndex = (int)(BinaryPrimitives.ReadUInt32LittleEndian(digest) % BucketCount);
        return _buckets[bucketIndex].TryAcquire();
    }

    public readonly record struct RateLimitResult(bool IsAllowed, TimeSpan RetryAfter);

    private sealed class AttemptBucket
    {
        private readonly object _sync = new();
        private long _windowStartedAt = Stopwatch.GetTimestamp();
        private int _attempts;

        public RateLimitResult TryAcquire()
        {
            lock (_sync)
            {
                var now = Stopwatch.GetTimestamp();
                var elapsed = Stopwatch.GetElapsedTime(_windowStartedAt, now);
                if (elapsed >= Window)
                {
                    _windowStartedAt = now;
                    _attempts = 0;
                    elapsed = TimeSpan.Zero;
                }

                if (_attempts >= PermitLimit)
                {
                    return new RateLimitResult(false, Window - elapsed);
                }

                _attempts++;
                return new RateLimitResult(true, TimeSpan.Zero);
            }
        }
    }
}
