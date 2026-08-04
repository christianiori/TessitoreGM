using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.Memory;

public sealed class MemoryEngine
{
    private readonly WorldEventProcessor _processor = new();

    public NpcMemory Replay(
        EntityId observerId,
        WorldSnapshot initialWorld,
        IEnumerable<IWorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(initialWorld);
        ArgumentNullException.ThrowIfNull(events);

        var memory = NpcMemory.Empty(observerId);
        var world = initialWorld;

        foreach (var worldEvent in events)
        {
            if (CanObserve(observerId, world, worldEvent))
            {
                memory = memory.Remember(worldEvent);
            }

            world = _processor.Apply(world, worldEvent);
        }

        return memory;
    }

    private static bool CanObserve(
        EntityId observerId,
        WorldSnapshot world,
        IWorldEvent worldEvent) =>
        worldEvent switch
        {
            EntityEnteredLocation entered =>
                entered.EntityId == observerId ||
                world.GetLocation(observerId) == entered.LocationId,
            EntityLeftLocation left =>
                left.EntityId == observerId ||
                world.GetLocation(observerId) == left.LocationId,
            OrderRequested requested =>
                requested.CustomerId == observerId ||
                requested.ArtisanId == observerId,
            PaymentTransferred payment =>
                payment.PayerId == observerId ||
                payment.PayeeId == observerId ||
                IsOrderParticipant(observerId, world, payment.OrderId),
            OrderAccepted accepted =>
                IsOrderParticipant(observerId, world, accepted.OrderId),
            OrderWorkStarted started =>
                IsOrderParticipant(observerId, world, started.OrderId),
            OrderCompleted completed =>
                IsOrderParticipant(observerId, world, completed.OrderId),
            OrderDelivered delivered =>
                IsOrderParticipant(observerId, world, delivered.OrderId),
            FactShared shared =>
                shared.SpeakerId == observerId ||
                shared.ListenerId == observerId,
            FactRevealed revealed => revealed.EntityId == observerId,
            TradeCompleted trade =>
                trade.BuyerId == observerId ||
                trade.SellerId == observerId,
            NeedIncreased increased => increased.EntityId == observerId,
            ResourceConsumed consumed => consumed.EntityId == observerId,
            ResourceProduced produced => produced.ProducerId == observerId,
            _ => false
        };

    private static bool IsOrderParticipant(
        EntityId observerId,
        WorldSnapshot world,
        OrderId orderId)
    {
        var order = world.GetOrder(orderId);
        return order is not null &&
            (order.CustomerId == observerId || order.ArtisanId == observerId);
    }
}
