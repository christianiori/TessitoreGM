using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class PhaseFiveCompletionTests
{
    [Fact]
    public void Advance_ProducerAtWork_ReplenishesStockToTargetOnce()
    {
        var farmerId = new EntityId("farmer");
        var fieldsId = new LocationId("fields");
        var grainId = new ResourceId("grain");
        var world = new WorldEventProcessor().Apply(
            WorldSnapshot.Create(
                At(3, 6, 0),
                new Dictionary<EntityId, int>(),
                new EntityResourceStock[]
                {
                    new(farmerId, grainId, 7)
                }),
            new EntityEnteredLocation(farmerId, fieldsId, At(3, 6, 30)));
        var farmer = new NpcAgent(
            farmerId,
            "farmer",
            NpcMemory.Empty(farmerId),
            new INpcBehavior[]
            {
                new DailyResourceProductionBehavior(
                    grainId,
                    fieldsId,
                    TimeSpan.FromHours(7),
                    10)
            });

        var result = new WorldSimulator(new IWorldRule[] { farmer })
            .Advance(world, At(3, 12, 0));

        var produced = Assert.IsType<ResourceProduced>(
            Assert.Single(result.ProducedEvents.OfType<ResourceProduced>()));
        Assert.Equal(3, produced.Quantity);
        Assert.Equal(10, result.World.GetResourceQuantity(farmerId, grainId));
    }

    [Fact]
    public void Apply_ProductionOutsideRequiredLocation_RejectsEvent()
    {
        var farmerId = new EntityId("farmer");
        var grainId = new ResourceId("grain");
        var fieldsId = new LocationId("fields");
        var world = WorldSnapshot.Create(
            At(3, 6, 0),
            new Dictionary<EntityId, int>());

        Assert.Throws<InvalidOperationException>(() =>
            new WorldEventProcessor().Apply(
                world,
                new ResourceProduced(
                    farmerId,
                    grainId,
                    3,
                    fieldsId,
                    At(3, 7, 0))));
    }

    [Fact]
    public void SerializeThenReplay_Production_PreservesStock()
    {
        var farmerId = new EntityId("farmer");
        var grainId = new ResourceId("grain");
        var fieldsId = new LocationId("fields");
        var log = new WorldEventLog(
            new WorldInitialState(
                At(3, 6, 0),
                Array.Empty<EntityBalance>(),
                new EntityResourceStock[]
                {
                    new(farmerId, grainId, 7)
                }),
            new IWorldEvent[]
            {
                new EntityEnteredLocation(
                    farmerId,
                    fieldsId,
                    At(3, 6, 30)),
                new ResourceProduced(
                    farmerId,
                    grainId,
                    3,
                    fieldsId,
                    At(3, 7, 0))
            });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var initialWorld = WorldSnapshot.Create(
            restored.InitialWorld.CurrentTime,
            new Dictionary<EntityId, int>(),
            restored.InitialWorld.ResourceStocks!);
        var world = new WorldEventProcessor().Replay(
            initialWorld,
            restored.Events);

        Assert.IsType<ResourceProduced>(restored.Events[1]);
        Assert.Equal(10, world.GetResourceQuantity(farmerId, grainId));
    }

    [Fact]
    public void Advance_FactPassesThroughTwoNpcs_ChangesFinalDecision()
    {
        var sourceId = new EntityId("source");
        var messengerId = new EntityId("messenger");
        var listenerId = new EntityId("listener");
        var squareId = new LocationId("square");
        var meetingId = new LocationId("meeting");
        var factId = new FactId("market-open");
        var initialWorld = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                At(3, 9, 0),
                new Dictionary<EntityId, int>()),
            new IWorldEvent[]
            {
                new EntityEnteredLocation(sourceId, squareId, At(3, 9, 0)),
                new EntityEnteredLocation(messengerId, squareId, At(3, 9, 0)),
                new EntityEnteredLocation(listenerId, squareId, At(3, 9, 0))
            });
        var source = new NpcAgent(
            sourceId,
            "source",
            NpcMemory.Empty(sourceId).LearnFact(factId),
            new INpcBehavior[]
            {
                new ShareFactWhenColocatedBehavior(messengerId, factId)
            });
        var messenger = new NpcAgent(
            messengerId,
            "messenger",
            NpcMemory.Empty(messengerId),
            new INpcBehavior[]
            {
                new ShareFactWhenColocatedBehavior(listenerId, factId)
            });
        var listener = new NpcAgent(
            listenerId,
            "listener",
            NpcMemory.Empty(listenerId),
            new INpcBehavior[]
            {
                new KnowledgeConditionalLocationBehavior(
                    meetingId,
                    TimeSpan.FromHours(10),
                    factId)
            });

        var result = new WorldSimulator(
            new IWorldRule[] { source, messenger, listener })
            .Advance(initialWorld, At(3, 10, 0));

        var sharings = result.ProducedEvents.OfType<FactShared>().ToArray();
        Assert.Equal(2, sharings.Length);
        Assert.Equal(sourceId, sharings[0].SpeakerId);
        Assert.Equal(messengerId, sharings[1].SpeakerId);
        Assert.Equal(listenerId, sharings[1].ListenerId);
        Assert.Equal(meetingId, result.World.GetLocation(listenerId));

        var listenerMemory = new MemoryEngine().Replay(
            listenerId,
            initialWorld,
            result.ProducedEvents);
        var learnedFromMessenger = Assert.Single(
            listenerMemory.ObservedEvents.OfType<FactShared>());
        Assert.Equal(messengerId, learnedFromMessenger.SpeakerId);
        Assert.Equal(At(3, 9, 0), learnedFromMessenger.OccurredAt);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);
}
