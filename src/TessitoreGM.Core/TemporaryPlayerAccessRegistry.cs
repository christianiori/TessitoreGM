using System.Security.Cryptography;
using System.Text;

namespace TessitoreGM.Core;

public sealed class TemporaryPlayerAccessRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<EntityId, PendingAccess> _pending = new();
    private readonly Dictionary<string, PlayerSession> _sessions =
        new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _failedAttempts = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _sessionLifetime;
    private DateTimeOffset? _lockedUntil;

    public TemporaryPlayerAccessRegistry()
        : this(TimeProvider.System, TimeSpan.FromHours(12))
    {
    }

    public TemporaryPlayerAccessRegistry(
        TimeProvider timeProvider,
        TimeSpan sessionLifetime)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(
            nameof(timeProvider));
        if (sessionLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sessionLifetime),
                "The session lifetime must be greater than zero.");
        }

        _sessionLifetime = sessionLifetime;
    }

    public string IssueCode(EntityId entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId.Value))
        {
            throw new ArgumentException(
                "The player entity id cannot be empty.",
                nameof(entityId));
        }

        lock (_sync)
        {
            CleanupExpired(_timeProvider.GetUtcNow());
            RevokeUnsafe(entityId);

            string code;
            do
            {
                code = RandomNumberGenerator
                    .GetInt32(10_000_000, 100_000_000)
                    .ToString();
            }
            while (_pending.Values.Any(access => access.Code == code));

            _pending[entityId] = new PendingAccess(code);
            return code;
        }
    }

    public bool TryExchangeCode(
        string suppliedCode,
        out EntityId entityId,
        out string sessionToken)
    {
        entityId = default;
        sessionToken = string.Empty;
        var now = _timeProvider.GetUtcNow();

        lock (_sync)
        {
            CleanupExpired(now);
            if (_lockedUntil is DateTimeOffset lockedUntil &&
                lockedUntil > now)
            {
                return false;
            }

            _lockedUntil = null;
            CleanupFailedAttempts(now);
            var supplied = suppliedCode?.Trim() ?? string.Empty;
            var match = _pending.FirstOrDefault(entry =>
                FixedEquals(supplied, entry.Value.Code));
            if (match.Equals(default(KeyValuePair<EntityId, PendingAccess>)))
            {
                RegisterFailure(now);
                return false;
            }

            _pending.Remove(match.Key);
            _failedAttempts.Clear();
            entityId = match.Key;
            sessionToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));
            _sessions[sessionToken] = new PlayerSession(
                entityId,
                now.Add(_sessionLifetime));
            return true;
        }
    }

    public bool VerifySession(EntityId entityId, string suppliedToken)
    {
        lock (_sync)
        {
            CleanupExpired(_timeProvider.GetUtcNow());
            return _sessions.TryGetValue(suppliedToken, out var session) &&
                session.EntityId == entityId;
        }
    }

    public bool TryResolveSession(
        string suppliedToken,
        out EntityId entityId)
    {
        lock (_sync)
        {
            CleanupExpired(_timeProvider.GetUtcNow());
            if (_sessions.TryGetValue(suppliedToken, out var session))
            {
                entityId = session.EntityId;
                return true;
            }

            entityId = default;
            return false;
        }
    }

    public void Revoke(EntityId entityId)
    {
        lock (_sync)
        {
            RevokeUnsafe(entityId);
        }
    }

    public void RevokeAll()
    {
        lock (_sync)
        {
            _pending.Clear();
            _sessions.Clear();
            _failedAttempts.Clear();
            _lockedUntil = null;
        }
    }

    private void RevokeUnsafe(EntityId entityId)
    {
        _pending.Remove(entityId);
        foreach (var token in _sessions
                     .Where(entry => entry.Value.EntityId == entityId)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _sessions.Remove(token);
        }
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        foreach (var token in _sessions
                     .Where(entry => entry.Value.ExpiresAt <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _sessions.Remove(token);
        }
    }

    private void CleanupFailedAttempts(DateTimeOffset now)
    {
        while (_failedAttempts.TryPeek(out var failedAt) &&
               failedAt < now.AddMinutes(-5))
        {
            _failedAttempts.Dequeue();
        }
    }

    private void RegisterFailure(DateTimeOffset now)
    {
        _failedAttempts.Enqueue(now);
        if (_failedAttempts.Count < 5)
        {
            return;
        }

        _failedAttempts.Clear();
        _lockedUntil = now.AddMinutes(1);
    }

    private static bool FixedEquals(string supplied, string expected)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(
            suppliedHash,
            expectedHash);
    }

    private sealed record PendingAccess(string Code);

    private sealed record PlayerSession(
        EntityId EntityId,
        DateTimeOffset ExpiresAt);
}
