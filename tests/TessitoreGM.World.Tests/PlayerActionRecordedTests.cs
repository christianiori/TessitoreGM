using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;

namespace TessitoreGM.World.Tests;

public sealed class PlayerActionRecordedTests
{
    [Fact]
    public void SerializeReplayAndNarrate_PlayerAction_PreservesChronicleEntry()
    {
        var occurredAt = new DateTimeOffset(
            2026, 8, 3, 14, 0, 0, TimeSpan.Zero);
        var playerAction = new PlayerActionRecorded(
            "Arianna",
            "Convince la guardia ad aprire il cancello.",
            occurredAt);
        var log = new WorldEventLog(
            new WorldInitialState(
                occurredAt.AddHours(-1),
                Array.Empty<EntityBalance>()),
            new IWorldEvent[] { playerAction });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                occurredAt.AddHours(-1),
                new Dictionary<EntityId, int>()),
            restored.Events);
        var line = Assert.Single(new DeterministicNarrator().Narrate(
            restored.Events,
            new NarrationContext()));

        Assert.Equal(playerAction, Assert.IsType<PlayerActionRecorded>(
            Assert.Single(restored.Events)));
        Assert.Equal(occurredAt, world.CurrentTime);
        Assert.Equal(
            "Arianna: Convince la guardia ad aprire il cancello.",
            line.Text);
    }
}
