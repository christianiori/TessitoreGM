using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;

namespace TessitoreGM.World.Tests;

public sealed class DeterministicNarratorTests
{
    private readonly EntityId _customerId = new("customer");
    private readonly EntityId _blacksmithId = new("blacksmith");
    private readonly EntityId _strangerId = new("stranger");
    private readonly LocationId _forgeId = new("forge");
    private readonly OrderId _orderId = new("order-1");

    [Fact]
    public void Narrate_CommissionEvents_ProducesFactualChronologicalLines()
    {
        var events = CreateEvents().Reverse().ToArray();
        var context = CreateContext();

        var lines = new DeterministicNarrator().Narrate(events, context);

        Assert.Equal(9, lines.Count);
        Assert.Equal(At(8, 10), lines[0].OccurredAt);
        Assert.Equal("Cliente entra in Forgia.", lines[0].Text);
        Assert.Equal("Cliente richiede martello a Fabbro.", lines[1].Text);
        Assert.Equal("Fabbro accetta l'ordine per 14 monete.", lines[2].Text);
        Assert.Equal("Cliente trasferisce 4 monete a Fabbro.", lines[3].Text);
        Assert.Equal("Fabbro inizia la lavorazione di martello.", lines[4].Text);
        Assert.Equal("Fabbro completa martello.", lines[5].Text);
        Assert.Equal("Fabbro consegna martello a Cliente.", lines[6].Text);
        Assert.Equal("Cliente condivide un'informazione con Viandante.", lines[7].Text);
        Assert.Equal("Cliente lascia Forgia.", lines[8].Text);
    }

    [Fact]
    public void Narrate_SameEventsTwice_ProducesSameChronicle()
    {
        var narrator = new DeterministicNarrator();
        var events = CreateEvents();
        var context = CreateContext();

        var first = narrator.Narrate(events, context);
        var second = narrator.Narrate(events, context);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Narrate_WorldTimeAdvanced_ProducesFactualLine()
    {
        var targetTime = At(13, 0);

        var line = Assert.Single(new DeterministicNarrator().Narrate(
            new IWorldEvent[] { new WorldTimeAdvanced(targetTime) },
            CreateContext()));

        Assert.Equal(targetTime, line.OccurredAt);
        Assert.Equal("Il tempo del mondo avanza.", line.Text);
    }

    private IWorldEvent[] CreateEvents() =>
        new IWorldEvent[]
        {
            new EntityEnteredLocation(_customerId, _forgeId, At(8, 10)),
            new OrderRequested(
                _orderId,
                _customerId,
                _blacksmithId,
                "martello",
                At(8, 11)),
            new OrderAccepted(_orderId, 14, At(8, 12)),
            new PaymentTransferred(
                _orderId,
                _customerId,
                _blacksmithId,
                4,
                At(8, 13)),
            new OrderWorkStarted(_orderId, At(8, 30)),
            new OrderCompleted(_orderId, new ItemId("hammer-1"), At(12, 30)),
            new OrderDelivered(_orderId, At(13, 0)),
            new FactShared(
                _customerId,
                _strangerId,
                FactId.ForOrder(_orderId),
                At(13, 1)),
            new EntityLeftLocation(_customerId, _forgeId, At(13, 2))
        };

    private NarrationContext CreateContext() =>
        new(
            new Dictionary<EntityId, string>
            {
                [_customerId] = "Cliente",
                [_blacksmithId] = "Fabbro",
                [_strangerId] = "Viandante"
            },
            new Dictionary<LocationId, string>
            {
                [_forgeId] = "Forgia"
            });

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 8, 3, hour, minute, 0, TimeSpan.Zero);
}
