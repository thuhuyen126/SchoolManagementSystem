using System.Security.Cryptography;
using System.Text;

namespace SchoolManagementSystem.Helpers;

public enum OtpValidationStatus
{
    Valid,
    Invalid,
    MissingOrExpired,
    AttemptsExceeded
}

public sealed record OtpChallengeInfo(string ChallengeId, string SubjectId, string AccountId, bool RememberMe);
public sealed record OtpValidationResult(OtpValidationStatus Status, OtpChallengeInfo? Challenge = null);

/// <summary>
/// Holds short-lived, single-use OTP challenges in process memory. A process restart
/// invalidates outstanding challenges; deployments with multiple instances need a
/// shared distributed implementation before enabling cross-instance login flows.
/// </summary>
public sealed class OtpChallengeStore
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, Challenge> _challenges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _latestByBinding = new(StringComparer.Ordinal);

    public OtpChallengeStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public OtpChallengeInfo Create(
        string purpose,
        string ownerId,
        string subjectId,
        string accountId,
        bool rememberMe,
        string code)
    {
        var binding = CreateBinding(purpose, subjectId);
        var challengeId = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var salt = RandomNumberGenerator.GetBytes(16);
        var codeHash = HashCode(salt, code);
        var info = new OtpChallengeInfo(challengeId, subjectId, accountId, rememberMe);

        lock (_gate)
        {
            foreach (var expiredId in _challenges
                .Where(pair => pair.Value.ExpiresAt <= _timeProvider.GetUtcNow())
                .Select(pair => pair.Key)
                .ToArray())
            {
                Remove(expiredId);
            }

            if (_latestByBinding.TryGetValue(binding, out var previousId))
            {
                _challenges.Remove(previousId);
            }

            _challenges[challengeId] = new Challenge(
                purpose, ownerId, info, salt, codeHash, _timeProvider.GetUtcNow().Add(Lifetime), MaxAttempts);
            _latestByBinding[binding] = challengeId;
        }

        return info;
    }

    public OtpChallengeInfo? GetPending(string challengeId, string purpose, string ownerId)
    {
        lock (_gate)
        {
            if (!_challenges.TryGetValue(challengeId, out var challenge) ||
                challenge.ExpiresAt <= _timeProvider.GetUtcNow() ||
                !string.Equals(challenge.Purpose, purpose, StringComparison.Ordinal) ||
                !string.Equals(challenge.OwnerId, ownerId, StringComparison.Ordinal))
            {
                Remove(challengeId);
                return null;
            }

            return challenge.Info;
        }
    }

    public void Cancel(string challengeId)
    {
        lock (_gate)
        {
            Remove(challengeId);
        }
    }

    public OtpValidationResult ValidateAndConsume(
        string challengeId,
        string purpose,
        string ownerId,
        string subjectId,
        string accountId,
        string code)
    {
        lock (_gate)
        {
            if (!_challenges.TryGetValue(challengeId, out var challenge) ||
                challenge.ExpiresAt <= _timeProvider.GetUtcNow() ||
                !string.Equals(challenge.Purpose, purpose, StringComparison.Ordinal) ||
                !string.Equals(challenge.OwnerId, ownerId, StringComparison.Ordinal) ||
                !string.Equals(challenge.Info.SubjectId, subjectId, StringComparison.Ordinal) ||
                !string.Equals(challenge.Info.AccountId, accountId, StringComparison.Ordinal))
            {
                Remove(challengeId);
                return new OtpValidationResult(OtpValidationStatus.MissingOrExpired);
            }

            var candidate = HashCode(challenge.Salt, code ?? string.Empty);
            if (!CryptographicOperations.FixedTimeEquals(candidate, challenge.CodeHash))
            {
                challenge.AttemptsRemaining--;
                if (challenge.AttemptsRemaining <= 0)
                {
                    Remove(challengeId);
                    return new OtpValidationResult(OtpValidationStatus.AttemptsExceeded);
                }

                return new OtpValidationResult(OtpValidationStatus.Invalid);
            }

            var info = challenge.Info;
            Remove(challengeId);
            return new OtpValidationResult(OtpValidationStatus.Valid, info);
        }
    }

    private void Remove(string challengeId)
    {
        if (!_challenges.Remove(challengeId, out var challenge))
        {
            return;
        }

        var binding = CreateBinding(challenge.Purpose, challenge.Info.SubjectId);
        if (_latestByBinding.TryGetValue(binding, out var latestId) &&
            string.Equals(latestId, challengeId, StringComparison.Ordinal))
        {
            _latestByBinding.Remove(binding);
        }
    }

    private static string CreateBinding(string purpose, string subjectId) => $"{purpose}\0{subjectId}";

    private static byte[] HashCode(byte[] salt, string code)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var payload = new byte[salt.Length + codeBytes.Length];
        Buffer.BlockCopy(salt, 0, payload, 0, salt.Length);
        Buffer.BlockCopy(codeBytes, 0, payload, salt.Length, codeBytes.Length);
        return SHA256.HashData(payload);
    }

    private sealed class Challenge(
        string purpose,
        string ownerId,
        OtpChallengeInfo info,
        byte[] salt,
        byte[] codeHash,
        DateTimeOffset expiresAt,
        int attemptsRemaining)
    {
        public string Purpose { get; } = purpose;
        public string OwnerId { get; } = ownerId;
        public OtpChallengeInfo Info { get; } = info;
        public byte[] Salt { get; } = salt;
        public byte[] CodeHash { get; } = codeHash;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int AttemptsRemaining { get; set; } = attemptsRemaining;
    }
}
