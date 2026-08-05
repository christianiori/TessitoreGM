using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;

namespace TessitoreGM.World.Tests;

public sealed class CoinsTransferredTests
{
    private static readonly EntityId PayerId = new("baker");
    private static readonly EntityId PayeeId = new("pc:arianna");
    private static readonly DateTimeOffset At = new(
        2026, 8, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SerializeReplayAndNarrate_ValidTransfer_MovesCoins()
    {
        var transferred = new CoinsTransferred(
            PayerId,
            PayeeId,
            3,
            "Ricompensa",
            At);
        var log = new WorldEventLog(
            new WorldInitialState(
                At,
                new[] { new EntityBalance(PayerId, 10) }),
            new IWorldEvent[] { transferred });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                At,
                new Dictionary<EntityId, int> { [PayerId] = 10 }),
            restored.Events);
        var line = Assert.Single(new DeterministicNarrator().Narrate(
            restored.Events,
            new NarrationContext(new Dictionary<EntityId, string>
            {
                [PayerId] = "Elia",
                [PayeeId] = "Arianna"
            })));

        Assert.Equal(7, world.GetBalance(PayerId));
        Assert.Equal(3, world.GetBalance(PayeeId));
        Assert.Equal(
            "Elia dà 3 monete a Arianna: Ricompensa.",
            line.Text);
    }

    [Fact]
    public void Apply_InsufficientFunds_RejectsTransfer()
    {
        var world = WorldSnapshot.Create(
            At,
            new Dictionary<EntityId, int> { [PayerId] = 2 });

        Assert.Throws<InvalidOperationException>(() => world.Apply(
            new CoinsTransferred(
                PayerId,
                PayeeId,
                3,
                "Ricompensa",
                At)));
    }
}
