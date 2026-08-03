using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.Memory;

public sealed class NpcMemory
{
    private readonly IReadOnlyList<IWorldEvent> _observedEvents;

    private NpcMemory(
        EntityId ownerId,
        IReadOnlyList<IWorldEvent> observedEvents)
    {
        OwnerId = ownerId;
        _observedEvents = observedEvents;
    }

    public EntityId OwnerId { get; }

    public IReadOnlyList<IWorldEvent> ObservedEvents => _observedEvents;

    public static NpcMemory Empty(EntityId ownerId) =>
        new(ownerId, Array.Empty<IWorldEvent>());

    public NpcMemory Remember(IWorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        return new NpcMemory(
            OwnerId,
            _observedEvents.Append(worldEvent).ToArray());
    }

    public bool KnowsOrder(OrderId orderId) =>
        _observedEvents.Any(worldEvent => worldEvent switch
        {
            OrderRequested requested => requested.OrderId == orderId,
            OrderAccepted accepted => accepted.OrderId == orderId,
            PaymentTransferred payment => payment.OrderId == orderId,
            OrderWorkStarted started => started.OrderId == orderId,
            OrderCompleted completed => completed.OrderId == orderId,
            OrderDelivered delivered => delivered.OrderId == orderId,
            _ => false
        });
}
