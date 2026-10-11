using SchoolManagementSystem.Helpers;
using Xunit;

namespace SchoolManagementSystem.Tests;

public sealed class OtpChallengeStoreTests
{
    [Fact]
    public void ValidCode_IsSingleUse()
    {
        var store = new OtpChallengeStore();
        var challenge = Create(store);

        var result = Validate(store, challenge, "123456");
        var replay = Validate(store, challenge, "123456");

        Assert.Equal(OtpValidationStatus.Valid, result.Status);
        Assert.Equal(OtpValidationStatus.MissingOrExpired, replay.Status);
    }

    [Fact]
    public void ExpiredChallenge_CannotBeConsumed()
    {
        var clock = new TestTimeProvider();
        var store = new OtpChallengeStore(clock);
        var challenge = Create(store);
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(OtpValidationStatus.MissingOrExpired, Validate(store, challenge, "123456").Status);
    }

    [Fact]
    public void FifthWrongAttempt_ConsumesChallenge()
    {
        var store = new OtpChallengeStore();
        var challenge = Create(store);

        for (var attempt = 1; attempt < 5; attempt++)
        {
            Assert.Equal(OtpValidationStatus.Invalid, Validate(store, challenge, "000000").Status);
        }

        Assert.Equal(OtpValidationStatus.AttemptsExceeded, Validate(store, challenge, "000000").Status);
        Assert.Equal(OtpValidationStatus.MissingOrExpired, Validate(store, challenge, "123456").Status);
    }

    [Fact]
    public void NewChallenge_InvalidatesPreviousChallengeForSamePurposeAndSubject()
    {
        var store = new OtpChallengeStore();
        var first = Create(store);
        var second = Create(store);

        Assert.Null(store.GetPending(first.ChallengeId, "login", "session-1"));
        Assert.Equal(OtpValidationStatus.Valid, Validate(store, second, "123456").Status);
    }

    [Theory]
    [InlineData("other-purpose", "session-1", "user-1", "account-1")]
    [InlineData("login", "other-session", "user-1", "account-1")]
    [InlineData("login", "session-1", "other-user", "account-1")]
    [InlineData("login", "session-1", "user-1", "other-account")]
    public void Challenge_IsBoundToPurposeOwnerSubjectAndAccount(
        string purpose,
        string owner,
        string subject,
        string account)
    {
        var store = new OtpChallengeStore();
        var challenge = Create(store);

        var result = store.ValidateAndConsume(
            challenge.ChallengeId, purpose, owner, subject, account, "123456");

        Assert.Equal(OtpValidationStatus.MissingOrExpired, result.Status);
    }

    private static OtpChallengeInfo Create(OtpChallengeStore store) =>
        store.Create("login", "session-1", "user-1", "account-1", false, "123456");

    private static OtpValidationResult Validate(OtpChallengeStore store, OtpChallengeInfo challenge, string code) =>
        store.ValidateAndConsume(
            challenge.ChallengeId, "login", "session-1", "user-1", "account-1", code);

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
