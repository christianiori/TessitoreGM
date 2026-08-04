using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class TrustChangedTests
{
    private readonly EntityId _subjectId = new("subject");
    private readonly EntityId _otherId = new("other");

    [Fact]
    public void Apply_PositiveAndNegativeChanges_UpdatesBoundedTrust()
    {
        IWorldEvent[] events =
        {
            new TrustChanged(_subjectId, _otherId, 80, "kept promise", At(10)),
            new TrustChanged(_subjectId, _otherId, 50, "offered help", At(11)),
            new TrustChanged(_subjectId, _otherId, -30, "lied", At(12))
        };

        var world = new WorldEventProcessor().Replay(CreateWorld(), events);

        Assert.Equal(100, world.GetTrust(_subjectId, _otherId));
        Assert.True(world.HasTrustReason(_subjectId, _otherId, "lied"));
    }

    [Fact]
    public void Apply_DuplicateReason_Throws()
    {
        var processor = new WorldEventProcessor();
        var world = processor.Apply(
            CreateWorld(),
            new TrustChanged(_subjectId, _otherId, 10, "shared news", At(10)));

        Assert.Throws<InvalidOperationException>(() => processor.Apply(
            world,
            new TrustChanged(_subjectId, _otherId, 10, "shared news", At(11))));
    }

    [Fact]
    public void Replay_TrustChanged_PreservesNegativeTrust()
    {
        var changed = new TrustChanged(
            _subjectId,
            _otherId,
            -20,
            "broke promise",
            At(10));
        var log = new WorldEventLog(
            new WorldInitialState(At(9), Array.Empty<EntityBalance>()),
            new IWorldEvent[] { changed });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));
        var world = new WorldEventProcessor().Replay(
            CreateWorld(),
            restored.Events);

        Assert.Equal(
            changed,
            Assert.IsType<TrustChanged>(Assert.Single(restored.Events)));
        Assert.Equal(-20, world.GetTrust(_subjectId, _otherId));
    }

    private WorldSnapshot CreateWorld() =>
        WorldSnapshot.Create(
            At(9),
            new Dictionary<EntityId, int>());

    private static DateTimeOffset At(int hour) =>
        new(2026, 8, 3, hour, 0, 0, TimeSpan.Zero);
}
