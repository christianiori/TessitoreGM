using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class DailyWeatherCycleRule : IWorldRule
{
    private readonly TimeSpan _timeOfDay;
    private readonly IReadOnlyList<WeatherCondition> _conditions;

    public DailyWeatherCycleRule(
        TimeSpan timeOfDay,
        IReadOnlyList<WeatherCondition> conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(timeOfDay));
        }
        if (conditions.Count < 2 ||
            conditions.Distinct().Count() != conditions.Count ||
            conditions.Any(condition => !Enum.IsDefined(condition)))
        {
            throw new ArgumentException(
                "A weather cycle requires at least two distinct conditions.",
                nameof(conditions));
        }

        _timeOfDay = timeOfDay;
        _conditions = conditions.ToArray();
    }

    public IWorldEvent? ProposeNext(
        WorldSnapshot world,
        DateTimeOffset until)
    {
        ArgumentNullException.ThrowIfNull(world);

        var scheduledAt = new DateTimeOffset(
            world.CurrentTime.Date.Add(_timeOfDay),
            world.CurrentTime.Offset);
        if (scheduledAt < world.CurrentTime ||
            world.LastWeatherChangedAt >= scheduledAt)
        {
            scheduledAt = scheduledAt.AddDays(1);
        }

        if (scheduledAt > until)
        {
            return null;
        }

        var currentIndex = _conditions
            .Select((condition, index) => (condition, index))
            .Where(entry => entry.condition == world.Weather)
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .First();
        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + 1) % _conditions.Count;
        return new WeatherChanged(_conditions[nextIndex], scheduledAt);
    }
}
