using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class TradeCompletedTests
{
    private readonly EntityId _buyerId = new("buyer");
    private readonly EntityId _sellerId = new("seller");
    private readonly ResourceId _grainId = new("grain");
    private readonly LocationId _marketId = new("market");

    [Fact]
    public void Apply_ValidTrade_TransfersMoneyAndResourceAtomically()
    {
        var world = new WorldEventProcessor().Apply(
            CreateWorld(12, 10),
            new TradeCompleted(
                _buyerId,
                _sellerId,
                _grainId,
                3,
                6,
                At(12)));

        Assert.Equal(6, world.GetBalance(_buyerId));
        Assert.Equal(6, world.GetBalance(_sellerId));
        Assert.Equal(3, world.GetResourceQuantity(_buyerId, _grainId));
        Assert.Equal(7, world.GetResourceQuantity(_sellerId, _grainId));
    }

    [Fact]
    public void Apply_InsufficientMoney_RejectsEntireTrade()
    {
        var original = CreateWorld(5, 10);
        var trade = new TradeCompleted(
            _buyerId,
            _sellerId,
            _grainId,
            3,
            6,
            At(12));

        Assert.Throws<InvalidOperationException>(() =>
            new WorldEventProcessor().Apply(original, trade));
        Assert.Equal(5, original.GetBalance(_buyerId));
        Assert.Equal(10, original.GetResourceQuantity(_sellerId, _grainId));
    }

    [Fact]
    public void Apply_InsufficientStock_RejectsEntireTrade()
    {
        var original = CreateWorld(12, 2);
        var trade = new TradeCompleted(
            _buyerId,
            _sellerId,
            _grainId,
            3,
            6,
            At(12));

        Assert.Throws<InvalidOperationException>(() =>
            new WorldEventProcessor().Apply(original, trade));
        Assert.Equal(12, original.GetBalance(_buyerId));
        Assert.Equal(2, original.GetResourceQuantity(_sellerId, _grainId));
    }

    [Fact]
    public void SerializeThenReplay_Trade_PreservesEconomicState()
    {
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(9),
                new EntityBalance[]
                {
                    new(_buyerId, 12),
                    new(_sellerId, 0)
                },
                new EntityResourceStock[]
                {
                    new(_sellerId, _grainId, 10)
                }),
            new IWorldEvent[]
            {
                new TradeCompleted(
                    _buyerId,
                    _sellerId,
                    _grainId,
                    3,
                    6,
                    At(12))
            });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(eventLog));
        var world = new WorldEventProcessor().Replay(
            CreateWorld(12, 10),
            restored.Events);

        Assert.IsType<TradeCompleted>(Assert.Single(restored.Events));
        Assert.Equal(6, world.GetBalance(_buyerId));
        Assert.Equal(3, world.GetResourceQuantity(_buyerId, _grainId));
        Assert.Equal(7, world.GetResourceQuantity(_sellerId, _grainId));
    }

    [Fact]
    public void Advance_ColocatedBuyerAndSeller_TradesOnlyOnce()
    {
        IWorldEvent[] arrivals =
        {
            new EntityEnteredLocation(_buyerId, _marketId, At(11)),
            new EntityEnteredLocation(_sellerId, _marketId, At(11))
        };
        var world = new WorldEventProcessor().Replay(
            CreateWorld(12, 10),
            arrivals);
        var buyer = new NpcAgent(
            _buyerId,
            "innkeeper",
            NpcMemory.Empty(_buyerId),
            new INpcBehavior[]
            {
                new TradeWhenColocatedBehavior(_sellerId, _grainId, 3, 6)
            });

        var result = new WorldSimulator(new IWorldRule[] { buyer })
            .Advance(world, At(13));

        Assert.Single(result.ProducedEvents.OfType<TradeCompleted>());
        Assert.Equal(6, result.World.GetBalance(_buyerId));
        Assert.Equal(3, result.World.GetResourceQuantity(_buyerId, _grainId));
    }

    private WorldSnapshot CreateWorld(int buyerMoney, int sellerStock) =>
        WorldSnapshot.Create(
            At(9),
            new Dictionary<EntityId, int>
            {
                [_buyerId] = buyerMoney,
                [_sellerId] = 0
            },
            new EntityResourceStock[]
            {
                new(_sellerId, _grainId, sellerStock)
            });

    private static DateTimeOffset At(int hour) =>
        new(2026, 8, 3, hour, 0, 0, TimeSpan.Zero);
}
