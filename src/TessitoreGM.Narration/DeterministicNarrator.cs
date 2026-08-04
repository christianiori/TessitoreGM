using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.Narration;

public sealed class DeterministicNarrator
{
    public IReadOnlyList<NarrationLine> Narrate(
        IEnumerable<IWorldEvent> events,
        NarrationContext context)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(context);

        var orders = new Dictionary<OrderId, OrderNarrationInfo>();
        var lines = new List<NarrationLine>();

        foreach (var worldEvent in events.OrderBy(worldEvent => worldEvent.OccurredAt))
        {
            if (worldEvent is OrderRequested requested)
            {
                orders[requested.OrderId] = new OrderNarrationInfo(
                    requested.CustomerId,
                    requested.ArtisanId,
                    requested.RequestedItem);
            }

            lines.Add(new NarrationLine(
                worldEvent.OccurredAt,
                Describe(worldEvent, context, orders)));
        }

        return lines;
    }

    private static string Describe(
        IWorldEvent worldEvent,
        NarrationContext context,
        IReadOnlyDictionary<OrderId, OrderNarrationInfo> orders) =>
        worldEvent switch
        {
            EntityEnteredLocation entered =>
                $"{context.Name(entered.EntityId)} entra in " +
                $"{context.Name(entered.LocationId)}.",
            EntityLeftLocation left =>
                $"{context.Name(left.EntityId)} lascia " +
                $"{context.Name(left.LocationId)}.",
            OrderRequested requested =>
                $"{context.Name(requested.CustomerId)} richiede " +
                $"{requested.RequestedItem} a {context.Name(requested.ArtisanId)}.",
            OrderAccepted accepted =>
                $"{ArtisanName(accepted.OrderId, context, orders)} accetta " +
                $"l'ordine per {accepted.TotalPrice} monete.",
            PaymentTransferred payment =>
                $"{context.Name(payment.PayerId)} trasferisce {payment.Amount} " +
                $"monete a {context.Name(payment.PayeeId)}.",
            OrderWorkStarted started =>
                $"{ArtisanName(started.OrderId, context, orders)} inizia la " +
                $"lavorazione di {ItemName(started.OrderId, orders)}.",
            OrderCompleted completed =>
                $"{ArtisanName(completed.OrderId, context, orders)} completa " +
                $"{ItemName(completed.OrderId, orders)}.",
            OrderDelivered delivered =>
                $"{ArtisanName(delivered.OrderId, context, orders)} consegna " +
                $"{ItemName(delivered.OrderId, orders)} a " +
                $"{CustomerName(delivered.OrderId, context, orders)}.",
            FactShared shared =>
                $"{context.Name(shared.SpeakerId)} condivide un'informazione " +
                $"con {context.Name(shared.ListenerId)}.",
            TrustChanged changed =>
                $"La fiducia di {context.Name(changed.SubjectId)} verso " +
                $"{context.Name(changed.OtherEntityId)} " +
                $"{(changed.Amount > 0 ? "aumenta" : "diminuisce")} " +
                $"di {Math.Abs(changed.Amount)}: {changed.Reason}.",
            TradeCompleted trade =>
                $"{context.Name(trade.BuyerId)} compra {trade.Quantity} " +
                $"unità di {context.Name(trade.ResourceId)} da " +
                $"{context.Name(trade.SellerId)} per " +
                $"{trade.TotalPrice} monete.",
            NeedIncreased increased =>
                $"Il bisogno di {context.Name(increased.NeedId)} di " +
                $"{context.Name(increased.EntityId)} aumenta di " +
                $"{increased.Amount}.",
            ResourceConsumed consumed =>
                $"{context.Name(consumed.EntityId)} consuma " +
                $"{consumed.Quantity} unità di " +
                $"{context.Name(consumed.ResourceId)}, riducendo il bisogno " +
                $"di {context.Name(consumed.NeedId)} di {consumed.Relief}.",
            ResourceProduced produced =>
                $"{context.Name(produced.ProducerId)} produce " +
                $"{produced.Quantity} unità di " +
                $"{context.Name(produced.ResourceId)} in " +
                $"{context.Name(produced.LocationId)}.",
            WorldTimeAdvanced =>
                "Il tempo del mondo avanza.",
            _ => throw new NotSupportedException(
                $"World event '{worldEvent.GetType().Name}' cannot be narrated.")
        };

    private static string ArtisanName(
        OrderId orderId,
        NarrationContext context,
        IReadOnlyDictionary<OrderId, OrderNarrationInfo> orders) =>
        orders.TryGetValue(orderId, out var order)
            ? context.Name(order.ArtisanId)
            : $"Artigiano dell'ordine {orderId}";

    private static string CustomerName(
        OrderId orderId,
        NarrationContext context,
        IReadOnlyDictionary<OrderId, OrderNarrationInfo> orders) =>
        orders.TryGetValue(orderId, out var order)
            ? context.Name(order.CustomerId)
            : $"Cliente dell'ordine {orderId}";

    private static string ItemName(
        OrderId orderId,
        IReadOnlyDictionary<OrderId, OrderNarrationInfo> orders) =>
        orders.TryGetValue(orderId, out var order)
            ? order.RequestedItem
            : $"l'oggetto dell'ordine {orderId}";

    private sealed record OrderNarrationInfo(
        EntityId CustomerId,
        EntityId ArtisanId,
        string RequestedItem);
}
