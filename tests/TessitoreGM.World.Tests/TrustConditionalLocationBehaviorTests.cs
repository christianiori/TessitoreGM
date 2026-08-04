using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class TrustConditionalLocationBehaviorTests
{
    private readonly EntityId _visitorId = new("visitor");
    private readonly EntityId _trustedId = new("trusted");
    private readonly LocationId _destinationId = new("farmstead");
    private readonly FactId _factId = new("village:market-open");

    [Fact]
    public void Advance_RequiredTrustExists_VisitsConfiguredDestination()
    {
        var world = new WorldEventProcessor().Apply(
            CreateWorld(),
            new FactShared(_trustedId, _visitorId, _factId, At(10, 0)));

        var result = CreateSimulator().Advance(world, At(12, 0));

        var arrival = Assert.IsType<EntityEnteredLocation>(
            result.ProducedEvents.First());
        Assert.Equal(_destinationId, arrival.LocationId);
        Assert.Equal(At(11, 0), arrival.OccurredAt);
        Assert.Equal(1, result.World.GetTrust(_visitorId, _trustedId));
    }

    [Fact]
    public void Advance_RequiredTrustMissing_DoesNotVisitDestination()
    {
        var result = CreateSimulator().Advance(CreateWorld(), At(12, 0));

        Assert.IsType<WorldTimeAdvanced>(Assert.Single(result.ProducedEvents));
        Assert.Null(result.World.GetLocation(_visitorId));
        Assert.Equal(0, result.World.GetTrust(_visitorId, _trustedId));
    }

    private WorldSimulator CreateSimulator()
    {
        var visitor = new NpcAgent(
            _visitorId,
            "visitor",
            NpcMemory.Empty(_visitorId),
            new INpcBehavior[]
            {
                new TrustConditionalLocationBehavior(
                    _trustedId,
                    _destinationId,
                    new TimeSpan(11, 0, 0),
                    minimumTrust: 1)
            });
        return new WorldSimulator(new IWorldRule[] { visitor });
    }

    private WorldSnapshot CreateWorld() =>
        WorldSnapshot.Create(
            At(9, 0),
            new Dictionary<EntityId, int>());

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
