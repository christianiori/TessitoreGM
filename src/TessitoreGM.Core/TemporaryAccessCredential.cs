using System.Security.Cryptography;
using System.Text;

namespace TessitoreGM.Core;

public sealed class TemporaryAccessCredential
{
    private readonly object _attemptLock = new();
    private readonly Queue<DateTimeOffset> _failedAttempts = new();
    private DateTimeOffset? _lockedUntil;

    public TemporaryAccessCredential()
    {
        AccessCode = RandomNumberGenerator
            .GetInt32(10_000_000, 100_000_000)
            .ToString();
        SessionToken = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
    }

    public string AccessCode { get; }

    public string SessionToken { get; }

    public bool TryVerifyCode(string suppliedCode)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_attemptLock)
        {
            if (_lockedUntil is DateTimeOffset lockedUntil &&
                lockedUntil > now)
            {
                return false;
            }

            _lockedUntil = null;
            while (_failedAttempts.TryPeek(out var failedAt) &&
                   failedAt < now.AddMinutes(-5))
            {
                _failedAttempts.Dequeue();
            }

            if (!FixedEquals(suppliedCode.Trim(), AccessCode))
            {
                _failedAttempts.Enqueue(now);
                if (_failedAttempts.Count >= 5)
                {
                    _failedAttempts.Clear();
                    _lockedUntil = now.AddMinutes(1);
                }
                return false;
            }

            _failedAttempts.Clear();
            return true;
        }
    }

    public bool VerifySession(string suppliedToken) =>
        FixedEquals(suppliedToken, SessionToken);

    private static bool FixedEquals(string supplied, string expected)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(
            suppliedHash,
            expectedHash);
    }
}
