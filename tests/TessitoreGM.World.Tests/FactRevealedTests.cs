using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Narration;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class FactRevealedTests
{
    private readonly EntityId _npcId = new("innkeeper");
    private readonly FactId _factId = new("village:market-open");

    [Fact]
    public void SerializeReplayAndRemember_RevealedFact_PreservesKnowledge()
    {
        var revealed = new FactRevealed(_npcId, _factId, At(9));
        var log = new WorldEventLog(
            new WorldInitialState(
                At(8),
                Array.Empty<EntityBalance>()),
            new IWorldEvent[] { revealed });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                At(8),
                new Dictionary<EntityId, int>()),
            restored.Events);
        var memory = new MemoryEngine().Replay(
            _npcId,
            WorldSnapshot.Create(
                At(8),
                new Dictionary<EntityId, int>()),
            restored.Events);

        Assert.Equal(revealed, Assert.IsType<FactRevealed>(
            Assert.Single(restored.Events)));
        Assert.Equal(At(9), world.CurrentTime);
        Assert.True(memory.KnowsFact(_factId));
    }

    [Fact]
    public void Advance_AfterFactRevealed_UsesKnowledgeInAutonomousDecision()
    {
        var marketId = new LocationId("market");
        var revealed = new FactRevealed(_npcId, _factId, At(9));
        var log = new WorldEventLog(
            new WorldInitialState(
                At(8),
                Array.Empty<EntityBalance>()),
            new IWorldEvent[] { revealed },
            new WorldSimulationDefinition(
                new[]
                {
                    new NpcSimulationDefinition(
                        _npcId,
                        "innkeeper",
                        Array.Empty<ScheduledArrivalDefinition>(),
                        Array.Empty<OrderProductionDefinition>(),
                        KnowledgeConditionalLocations: new[]
                        {
                            new KnowledgeConditionalLocationDefinition(
                                marketId,
                                TimeSpan.FromHours(10),
                                _factId)
                        })
                }));
        var world = new WorldEventProcessor().Replay(
            WorldSnapshot.Create(
                At(8),
                new Dictionary<EntityId, int>()),
            log.Events);

        var result = new ConfiguredWorldSimulator().Advance(
            log,
            world,
            At(10));

        var arrival = Assert.Single(
            result.ProducedEvents.OfType<EntityEnteredLocation>());
        Assert.Equal(marketId, arrival.LocationId);
        Assert.Equal(At(10), arrival.OccurredAt);
    }

    [Fact]
    public void Narrate_FactRevealed_DescribesLearningWithoutInventingSpeaker()
    {
        var line = Assert.Single(new DeterministicNarrator().Narrate(
            new IWorldEvent[]
            {
                new FactRevealed(_npcId, _factId, At(9))
            },
            new NarrationContext(new Dictionary<EntityId, string>
            {
                [_npcId] = "Mira"
            })));

        Assert.Equal("Mira apprende una nuova informazione.", line.Text);
    }

    private static DateTimeOffset At(int hour) =>
        new(2026, 8, 3, hour, 0, 0, TimeSpan.Zero);
}
