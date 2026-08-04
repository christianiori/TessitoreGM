using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class BasicNeedsTests
{
    private readonly EntityId _npcId = new("npc");
    private readonly NeedId _hungerId = new("hunger");
    private readonly ResourceId _foodId = new("food");

    [Fact]
    public void Apply_NeedIncreaseAndConsumption_UpdatesNeedAndStock()
    {
        var processor = new WorldEventProcessor();
        var hungryWorld = processor.Apply(
            CreateWorld(2),
            new NeedIncreased(_npcId, _hungerId, 40, At(3, 8)));

        var fedWorld = processor.Apply(
            hungryWorld,
            new ResourceConsumed(
                _npcId,
                _foodId,
                1,
                _hungerId,
                40,
                At(3, 8)));

        Assert.Equal(0, fedWorld.GetNeedLevel(_npcId, _hungerId));
        Assert.Equal(1, fedWorld.GetResourceQuantity(_npcId, _foodId));
    }

    [Fact]
    public void Apply_NeedIncrease_StopsAtOneHundred()
    {
        var processor = new WorldEventProcessor();
        var world = processor.Apply(
            CreateWorld(0),
            new NeedIncreased(_npcId, _hungerId, 80, At(3, 8)));

        world = processor.Apply(
            world,
            new NeedIncreased(_npcId, _hungerId, 40, At(4, 8)));

        Assert.Equal(100, world.GetNeedLevel(_npcId, _hungerId));
    }

    [Fact]
    public void Apply_ConsumptionWithoutStock_RejectsEvent()
    {
        var processor = new WorldEventProcessor();
        var world = processor.Apply(
            CreateWorld(0),
            new NeedIncreased(_npcId, _hungerId, 40, At(3, 8)));

        Assert.Throws<InvalidOperationException>(() => processor.Apply(
            world,
            new ResourceConsumed(
                _npcId,
                _foodId,
                1,
                _hungerId,
                40,
                At(3, 8))));
        Assert.Equal(40, world.GetNeedLevel(_npcId, _hungerId));
    }

    [Fact]
    public void Advance_MultipleDays_GrowsAndRelievesNeedAutonomously()
    {
        var npc = new NpcAgent(
            _npcId,
            "villager",
            NpcMemory.Empty(_npcId),
            new INpcBehavior[]
            {
                new DailyNeedIncreaseBehavior(
                    _hungerId,
                    TimeSpan.FromHours(8),
                    40),
                new ConsumeResourceForNeedBehavior(
                    _hungerId,
                    _foodId,
                    40,
                    1,
                    40)
            });

        var result = new WorldSimulator(new IWorldRule[] { npc }).Advance(
            CreateWorld(2),
            At(5, 9));

        Assert.Equal(3, result.ProducedEvents.OfType<NeedIncreased>().Count());
        Assert.Equal(2, result.ProducedEvents.OfType<ResourceConsumed>().Count());
        Assert.Equal(40, result.World.GetNeedLevel(_npcId, _hungerId));
        Assert.Equal(0, result.World.GetResourceQuantity(_npcId, _foodId));
    }

    [Fact]
    public void SerializeThenReplay_NeedEvents_PreservesState()
    {
        IWorldEvent[] events =
        {
            new NeedIncreased(_npcId, _hungerId, 40, At(3, 8)),
            new ResourceConsumed(
                _npcId,
                _foodId,
                1,
                _hungerId,
                30,
                At(3, 9))
        };
        var log = new WorldEventLog(
            new WorldInitialState(
                At(3, 6),
                Array.Empty<EntityBalance>(),
                new EntityResourceStock[]
                {
                    new(_npcId, _foodId, 2)
                }),
            events);
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            CreateWorld(2),
            restored.Events);

        Assert.Equal(10, world.GetNeedLevel(_npcId, _hungerId));
        Assert.Equal(1, world.GetResourceQuantity(_npcId, _foodId));
        Assert.IsType<NeedIncreased>(restored.Events[0]);
        Assert.IsType<ResourceConsumed>(restored.Events[1]);
    }

    private WorldSnapshot CreateWorld(int food) =>
        WorldSnapshot.Create(
            At(3, 6),
            new Dictionary<EntityId, int>(),
            new EntityResourceStock[]
            {
                new(_npcId, _foodId, food)
            });

    private static DateTimeOffset At(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, TimeSpan.Zero);
}
