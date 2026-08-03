using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldSnapshotTests
{
    [Fact]
    public void Apply_EntityEnteredLocation_MovesEntityToLocation()
    {
        var customerId = new EntityId("customer");
        var forgeId = new LocationId("forge");
        var initialWorld = WorldSnapshot.Empty;
        var worldEvent = new EntityEnteredLocation(
            customerId,
            forgeId,
            new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero));

        var updatedWorld = initialWorld.Apply(worldEvent);

        Assert.Null(initialWorld.GetLocation(customerId));
        Assert.Equal(forgeId, updatedWorld.GetLocation(customerId));
    }
}
