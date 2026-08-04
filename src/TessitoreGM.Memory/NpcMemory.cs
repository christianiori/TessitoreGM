using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.Memory;

public sealed class NpcMemory
{
    private readonly IReadOnlyList<IWorldEvent> _observedEvents;
    private readonly IReadOnlySet<FactId> _knownFacts;

    private NpcMemory(
        EntityId ownerId,
        IReadOnlyList<IWorldEvent> observedEvents,
        IReadOnlySet<FactId> knownFacts)
    {
        OwnerId = ownerId;
        _observedEvents = observedEvents;
        _knownFacts = knownFacts;
    }

    public EntityId OwnerId { get; }

    public IReadOnlyList<IWorldEvent> ObservedEvents => _observedEvents;

    public IReadOnlySet<FactId> KnownFacts => _knownFacts;

    public static NpcMemory Empty(EntityId ownerId) =>
        new(ownerId, Array.Empty<IWorldEvent>(), new HashSet<FactId>());

    public NpcMemory Remember(IWorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);

        var knownFacts = new HashSet<FactId>(_knownFacts);
        var learnedFact = GetFactId(worldEvent);

        if (learnedFact is FactId factId)
        {
            knownFacts.Add(factId);
        }

        return new NpcMemory(
            OwnerId,
            _observedEvents.Append(worldEvent).ToArray(),
            knownFacts);
    }

    public bool KnowsFact(FactId factId) => _knownFacts.Contains(factId);

    public NpcMemory LearnFact(FactId factId)
    {
        var knownFacts = new HashSet<FactId>(_knownFacts) { factId };
        return new NpcMemory(OwnerId, _observedEvents, knownFacts);
    }

    public bool KnowsOrder(OrderId orderId) => KnowsFact(FactId.ForOrder(orderId));

    private FactId? GetFactId(IWorldEvent worldEvent) =>
        worldEvent switch
        {
            OrderRequested requested => FactId.ForOrder(requested.OrderId),
            OrderAccepted accepted => FactId.ForOrder(accepted.OrderId),
            PaymentTransferred payment => FactId.ForOrder(payment.OrderId),
            OrderWorkStarted started => FactId.ForOrder(started.OrderId),
            OrderCompleted completed => FactId.ForOrder(completed.OrderId),
            OrderDelivered delivered => FactId.ForOrder(delivered.OrderId),
            FactShared shared when shared.ListenerId == OwnerId => shared.FactId,
            FactShared shared when shared.SpeakerId == OwnerId => shared.FactId,
            _ => null
        };
}
