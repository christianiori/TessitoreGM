using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class BalanceConditionalLocationBehaviorTests
{
    private readonly EntityId _farmerId = new("farmer");
    private readonly LocationId _marketId = new("market");

    [Fact]
    public void Advance_LowOwnBalance_MovesNpcToConfiguredDestination()
    {
        var result = CreateSimulator().Advance(CreateWorld(balance: 3), At(13, 0));

        var arrival = Assert.IsType<EntityEnteredLocation>(
            result.ProducedEvents.First());
        Assert.Equal(_farmerId, arrival.EntityId);
        Assert.Equal(_marketId, arrival.LocationId);
        Assert.Equal(At(12, 0), arrival.OccurredAt);
        Assert.Equal(_marketId, result.World.GetLocation(_farmerId));
    }

    [Fact]
    public void Advance_SufficientOwnBalance_DoesNotMoveNpc()
    {
        var result = CreateSimulator().Advance(CreateWorld(balance: 8), At(13, 0));

        Assert.IsType<WorldTimeAdvanced>(Assert.Single(result.ProducedEvents));
        Assert.Null(result.World.GetLocation(_farmerId));
    }

    private WorldSimulator CreateSimulator()
    {
        var farmer = new NpcAgent(
            _farmerId,
            "farmer",
            NpcMemory.Empty(_farmerId),
            new INpcBehavior[]
            {
                new BalanceConditionalLocationBehavior(
                    _marketId,
                    new TimeSpan(12, 0, 0),
                    maximumBalance: 5)
            });

        return new WorldSimulator(new IWorldRule[] { farmer });
    }

    private WorldSnapshot CreateWorld(int balance) =>
        WorldSnapshot.Create(
            At(8, 0),
            new Dictionary<EntityId, int> { [_farmerId] = balance });

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
