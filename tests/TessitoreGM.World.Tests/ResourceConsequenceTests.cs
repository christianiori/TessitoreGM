using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;

namespace TessitoreGM.World.Tests;

public sealed class ResourceConsequenceTests
{
    private static readonly EntityId FarmerId = new("farmer");
    private static readonly EntityId PlayerId = new("pc:arianna");
    private static readonly ResourceId WheatId = new("wheat");
    private static readonly DateTimeOffset At = new(
        2026, 8, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SerializeReplayAndNarrate_ResourceConsequences_PreserveStocks()
    {
        var events = new IWorldEvent[]
        {
            new ResourceAcquired(PlayerId, WheatId, 2, "Raccolto", At),
            new ResourceTransferred(
                FarmerId,
                PlayerId,
                WheatId,
                2,
                "Dono",
                At),
            new ResourceLost(PlayerId, WheatId, 1, "Danneggiato", At)
        };
        var initialStocks = new[]
        {
            new EntityResourceStock(FarmerId, WheatId, 3)
        };
        var log = new WorldEventLog(
            new WorldInitialState(
                At,
                Array.Empty<EntityBalance>(),
                initialStocks),
            events);
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                At,
                new Dictionary<EntityId, int>(),
                initialStocks),
            restored.Events);
        var lines = new DeterministicNarrator().Narrate(
            restored.Events,
            new NarrationContext(
                new Dictionary<EntityId, string>
                {
                    [FarmerId] = "Brina",
                    [PlayerId] = "Arianna"
                },
                resourceNames: new Dictionary<ResourceId, string>
                {
                    [WheatId] = "grano"
                }));

        Assert.Equal(1, world.GetResourceQuantity(FarmerId, WheatId));
        Assert.Equal(3, world.GetResourceQuantity(PlayerId, WheatId));
        Assert.Equal(3, lines.Count);
        Assert.Contains("Raccolto", lines[0].Text);
        Assert.Contains("Dono", lines[1].Text);
        Assert.Contains("Danneggiato", lines[2].Text);
    }

    [Fact]
    public void Apply_ResourceLossAboveStock_RejectsConsequence()
    {
        var world = WorldSnapshot.Create(
            At,
            new Dictionary<EntityId, int>());

        Assert.Throws<InvalidOperationException>(() => world.Apply(
            new ResourceLost(PlayerId, WheatId, 1, "Perso", At)));
    }
}
