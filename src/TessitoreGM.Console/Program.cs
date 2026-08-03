using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

var customerId = new EntityId("customer");
var forgeId = new LocationId("forge");
var world = WorldSnapshot.Empty;

world = world.Apply(new EntityEnteredLocation(
    customerId,
    forgeId,
    DateTimeOffset.UtcNow));

Console.WriteLine($"{customerId} entered {world.GetLocation(customerId)}.");
