using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class ShareFactWhenColocatedBehaviorTests
{
    private readonly EntityId _speakerId = new("speaker");
    private readonly EntityId _listenerId = new("listener");
    private readonly LocationId _marketId = new("market");
    private readonly FactId _factId = new("village:market-open");

    [Fact]
    public void Advance_ColocatedNpcs_SharesKnownFactOnce()
    {
        var world = PlaceEntitiesTogether();
        var simulator = CreateSimulator(
            NpcMemory.Empty(_speakerId).LearnFact(_factId));

        var result = simulator.Advance(world, At(11, 0));

        var shared = Assert.IsType<FactShared>(result.ProducedEvents.First());
        Assert.Equal(_speakerId, shared.SpeakerId);
        Assert.Equal(_listenerId, shared.ListenerId);
        Assert.Equal(_factId, shared.FactId);
        Assert.True(result.World.HasSharedFact(
            _speakerId,
            _listenerId,
            _factId));
        Assert.Single(result.ProducedEvents.OfType<FactShared>());
        var listenerMemory = new MemoryEngine().Replay(
            _listenerId,
            world,
            result.ProducedEvents);
        Assert.True(listenerMemory.KnowsFact(_factId));
    }

    [Fact]
    public void Advance_SeparatedNpcs_DoesNotShareFact()
    {
        var world = new WorldEventProcessor().Apply(
            CreateWorld(),
            new EntityEnteredLocation(_speakerId, _marketId, At(10, 0)));
        var simulator = CreateSimulator(
            NpcMemory.Empty(_speakerId).LearnFact(_factId));

        var result = simulator.Advance(world, At(11, 0));

        Assert.Empty(result.ProducedEvents.OfType<FactShared>());
    }

    private WorldSimulator CreateSimulator(NpcMemory memory)
    {
        var speaker = new NpcAgent(
            _speakerId,
            "villager",
            memory,
            new INpcBehavior[]
            {
                new ShareFactWhenColocatedBehavior(_listenerId, _factId)
            });
        return new WorldSimulator(new IWorldRule[] { speaker });
    }

    private WorldSnapshot PlaceEntitiesTogether()
    {
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(_speakerId, _marketId, At(10, 0)),
            new EntityEnteredLocation(_listenerId, _marketId, At(10, 0))
        };
        return new WorldEventProcessor().Replay(CreateWorld(), events);
    }

    private WorldSnapshot CreateWorld() =>
        WorldSnapshot.Create(
            At(9, 0),
            new Dictionary<EntityId, int>());

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
