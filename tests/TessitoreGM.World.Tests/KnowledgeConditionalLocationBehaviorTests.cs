using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Memory;
using TessitoreGM.Npcs;

namespace TessitoreGM.World.Tests;

public sealed class KnowledgeConditionalLocationBehaviorTests
{
    private readonly EntityId _npcId = new("villager");
    private readonly LocationId _marketId = new("market");
    private readonly FactId _marketOpenFactId = new("village:market-open");

    [Fact]
    public void Advance_NpcKnowsRequiredFact_MovesToDestination()
    {
        var memory = NpcMemory.Empty(_npcId).LearnFact(_marketOpenFactId);

        var result = CreateSimulator(memory).Advance(CreateWorld(), At(13, 0));

        var arrival = Assert.IsType<EntityEnteredLocation>(
            result.ProducedEvents.First());
        Assert.Equal(_marketId, arrival.LocationId);
        Assert.Equal(At(12, 0), arrival.OccurredAt);
    }

    [Fact]
    public void Advance_NpcDoesNotKnowRequiredFact_DoesNotMove()
    {
        var memory = NpcMemory.Empty(_npcId);

        var result = CreateSimulator(memory).Advance(CreateWorld(), At(13, 0));

        Assert.IsType<WorldTimeAdvanced>(Assert.Single(result.ProducedEvents));
        Assert.Null(result.World.GetLocation(_npcId));
    }

    private WorldSimulator CreateSimulator(NpcMemory memory)
    {
        var npc = new NpcAgent(
            _npcId,
            "villager",
            memory,
            new INpcBehavior[]
            {
                new KnowledgeConditionalLocationBehavior(
                    _marketId,
                    new TimeSpan(12, 0, 0),
                    _marketOpenFactId)
            });
        return new WorldSimulator(new IWorldRule[] { npc });
    }

    private WorldSnapshot CreateWorld() =>
        WorldSnapshot.Create(
            At(8, 0),
            new Dictionary<EntityId, int>());

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
