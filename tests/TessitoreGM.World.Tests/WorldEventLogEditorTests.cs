using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldEventLogEditorTests
{
    [Fact]
    public void Append_EntityMovement_PersistsReplayableExternalEvent()
    {
        var npcId = new EntityId("innkeeper");
        var homeId = new LocationId("innkeeper-home");
        var innId = new LocationId("inn");
        var occurredAt = new DateTimeOffset(
            2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
        var initialWorld = WorldSnapshot.Create(
            occurredAt,
            new Dictionary<EntityId, int>());
        var initialArrival = new EntityEnteredLocation(
            npcId,
            homeId,
            occurredAt);
        var world = new WorldEventProcessor().Apply(
            initialWorld,
            initialArrival);
        var log = new WorldEventLog(
            new WorldInitialState(
                occurredAt,
                Array.Empty<EntityBalance>()),
            new IWorldEvent[] { initialArrival });
        var movement = new EntityEnteredLocation(
            npcId,
            innId,
            world.CurrentTime);

        var updatedLog = new WorldEventLogEditor().Append(
            log,
            world,
            movement);
        var replayed = new WorldEventProcessor().Replay(
            initialWorld,
            updatedLog.Events);

        Assert.Equal(2, updatedLog.Events.Count);
        Assert.Same(movement, updatedLog.Events[1]);
        Assert.Equal(innId, replayed.GetLocation(npcId));
        Assert.Equal(occurredAt, replayed.CurrentTime);
    }
}
