using TessitoreGM.Core;

namespace TessitoreGM.Events;

public sealed record WeatherChanged(
    WeatherCondition Condition,
    DateTimeOffset OccurredAt) : IWorldEvent;
