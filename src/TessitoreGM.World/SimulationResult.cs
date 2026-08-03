using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed record SimulationResult(
    WorldSnapshot World,
    IReadOnlyList<IWorldEvent> ProducedEvents);
