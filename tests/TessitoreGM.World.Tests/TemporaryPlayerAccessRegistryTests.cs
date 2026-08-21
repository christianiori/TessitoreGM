using TessitoreGM.Core;

namespace TessitoreGM.World.Tests;

public sealed class TemporaryPlayerAccessRegistryTests
{
    [Fact]
    public void IssuedCode_CreatesSessionForOnlyItsPlayer()
    {
        var registry = new TemporaryPlayerAccessRegistry();
        var player = new EntityId("pc:arianna");
        var otherPlayer = new EntityId("pc:borin");
        var code = registry.IssueCode(player);

        Assert.Equal(8, code.Length);
        Assert.True(code.All(char.IsDigit));
        Assert.True(registry.TryExchangeCode(
            code,
            out var authorizedPlayer,
            out var sessionToken));
        Assert.Equal(player, authorizedPlayer);
        Assert.True(registry.VerifySession(player, sessionToken));
        Assert.False(registry.VerifySession(otherPlayer, sessionToken));
        Assert.True(registry.TryResolveSession(
            sessionToken,
            out var resolvedPlayer));
        Assert.Equal(player, resolvedPlayer);
    }

    [Fact]
    public void ExchangedCode_CannotBeUsedTwice()
    {
        var registry = new TemporaryPlayerAccessRegistry();
        var code = registry.IssueCode(new EntityId("pc:arianna"));

        Assert.True(registry.TryExchangeCode(code, out _, out _));
        Assert.False(registry.TryExchangeCode(code, out _, out _));
    }

    [Fact]
    public void NewCode_RevokesPreviousCodeAndSession()
    {
        var registry = new TemporaryPlayerAccessRegistry();
        var player = new EntityId("pc:arianna");
        var firstCode = registry.IssueCode(player);
        Assert.True(registry.TryExchangeCode(
            firstCode,
            out _,
            out var firstSession));

        var secondCode = registry.IssueCode(player);

        Assert.False(registry.VerifySession(player, firstSession));
        Assert.False(registry.TryExchangeCode(firstCode, out _, out _));
        Assert.True(registry.TryExchangeCode(secondCode, out _, out _));
    }

    [Fact]
    public void ExpiredSession_IsRejected()
    {
        var time = new AdjustableTimeProvider();
        var registry = new TemporaryPlayerAccessRegistry(
            time,
            TimeSpan.FromHours(12));
        var player = new EntityId("pc:arianna");
        var code = registry.IssueCode(player);
        Assert.True(registry.TryExchangeCode(
            code,
            out _,
            out var sessionToken));

        time.Advance(TimeSpan.FromHours(13));

        Assert.False(registry.VerifySession(player, sessionToken));
    }

    [Fact]
    public void FiveFailedCodes_TemporarilyBlockCorrectCode()
    {
        var registry = new TemporaryPlayerAccessRegistry();
        var code = registry.IssueCode(new EntityId("pc:arianna"));
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False(registry.TryExchangeCode(
                "00000000",
                out _,
                out _));
        }

        Assert.False(registry.TryExchangeCode(code, out _, out _));
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
