using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class WeatherCycleTests
{
    private static readonly WeatherCondition[] Conditions =
    {
        WeatherCondition.Clear,
        WeatherCondition.Cloudy,
        WeatherCondition.Rain,
        WeatherCondition.Storm
    };

    [Fact]
    public void Apply_WeatherChanged_UpdatesPersistentWeatherState()
    {
        var initialTime = At(3, 5);
        var changedAt = At(3, 6);
        var world = WorldSnapshot.Create(
            initialTime,
            new Dictionary<EntityId, int>(),
            Array.Empty<EntityResourceStock>(),
            WeatherCondition.Clear);

        var updated = new WorldEventProcessor().Apply(
            world,
            new WeatherChanged(WeatherCondition.Cloudy, changedAt));

        Assert.Equal(WeatherCondition.Cloudy, updated.Weather);
        Assert.Equal(changedAt, updated.LastWeatherChangedAt);
        Assert.Equal(changedAt, updated.CurrentTime);
    }

    [Fact]
    public void Advance_InTwoIntervals_MatchesSingleInterval()
    {
        var initialWorld = WorldSnapshot.Create(
            At(3, 5),
            new Dictionary<EntityId, int>());
        var rule = new DailyWeatherCycleRule(
            TimeSpan.FromHours(6),
            Conditions);
        var simulator = new WorldSimulator(new[] { rule });

        var firstInterval = simulator.Advance(initialWorld, At(3, 12));
        var secondInterval = simulator.Advance(firstInterval.World, At(4, 7));
        var singleInterval = simulator.Advance(initialWorld, At(4, 7));

        Assert.Equal(WeatherCondition.Rain, secondInterval.World.Weather);
        Assert.Equal(singleInterval.World.Weather, secondInterval.World.Weather);
        Assert.Equal(
            singleInterval.World.LastWeatherChangedAt,
            secondInterval.World.LastWeatherChangedAt);
        Assert.Equal(singleInterval.World.CurrentTime, secondInterval.World.CurrentTime);
    }

    [Fact]
    public void ConfiguredSimulator_ProducesDailyWeatherEvent()
    {
        var initialTime = At(3, 5);
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                initialTime,
                Array.Empty<EntityBalance>(),
                Weather: WeatherCondition.Clear),
            Array.Empty<IWorldEvent>(),
            new WorldSimulationDefinition(
                Array.Empty<NpcSimulationDefinition>(),
                WeatherCycle: new DailyWeatherCycleDefinition(
                    TimeSpan.FromHours(6),
                    Conditions)));
        var world = WorldSnapshot.Create(
            initialTime,
            new Dictionary<EntityId, int>());

        var result = new ConfiguredWorldSimulator().Advance(
            eventLog,
            world,
            At(3, 7));

        var changed = Assert.Single(
            result.ProducedEvents.OfType<WeatherChanged>());
        Assert.Equal(WeatherCondition.Cloudy, changed.Condition);
        Assert.Equal(At(3, 6), changed.OccurredAt);
        Assert.Equal(WeatherCondition.Cloudy, result.World.Weather);
    }

    [Fact]
    public void SerializeReplayAndNarrate_PreservesWeatherEvent()
    {
        var changed = new WeatherChanged(
            WeatherCondition.Storm,
            At(3, 6));
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(3, 5),
                Array.Empty<EntityBalance>(),
                Weather: WeatherCondition.Rain),
            new IWorldEvent[] { changed },
            new WorldSimulationDefinition(
                Array.Empty<NpcSimulationDefinition>(),
                WeatherCycle: new DailyWeatherCycleDefinition(
                    TimeSpan.FromHours(6),
                    Conditions)));
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(eventLog));
        var initialWorld = WorldSnapshot.Create(
            restored.InitialWorld.CurrentTime,
            new Dictionary<EntityId, int>(),
            Array.Empty<EntityResourceStock>(),
            restored.InitialWorld.Weather);
        var replayed = new WorldEventProcessor().Replay(
            initialWorld,
            restored.Events);
        var narration = new DeterministicNarrator().Narrate(
            restored.Events,
            new NarrationContext(
                new Dictionary<EntityId, string>(),
                new Dictionary<LocationId, string>(),
                new Dictionary<ResourceId, string>(),
                new Dictionary<NeedId, string>()));

        Assert.Equal(WeatherCondition.Rain, restored.InitialWorld.Weather);
        Assert.Equal(changed, Assert.IsType<WeatherChanged>(Assert.Single(restored.Events)));
        Assert.Equal(WeatherCondition.Storm, replayed.Weather);
        Assert.Equal("Sul territorio si abbatte una tempesta.", Assert.Single(narration).Text);
    }

    private static DateTimeOffset At(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, TimeSpan.Zero);
}
