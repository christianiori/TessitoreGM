using TessitoreGM.Events;

namespace TessitoreGM.World;

public sealed class WorldEventProcessor
{
    public WorldSnapshot Apply(WorldSnapshot world, IWorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(worldEvent);

        return worldEvent switch
        {
            EntityEnteredLocation entered => world.Apply(entered),
            EntityLeftLocation left => world.Apply(left),
            OrderRequested requested => world.Apply(requested),
            OrderAccepted accepted => world.Apply(accepted),
            PaymentTransferred payment => world.Apply(payment),
            CoinsTransferred coins => world.Apply(coins),
            OrderWorkStarted workStarted => world.Apply(workStarted),
            OrderCompleted completed => world.Apply(completed),
            OrderDelivered delivered => world.Apply(delivered),
            FactShared shared => world.Apply(shared),
            FactRevealed revealed => world.Apply(revealed),
            TrustChanged trustChanged => world.Apply(trustChanged),
            TradeCompleted trade => world.Apply(trade),
            NeedIncreased increased => world.Apply(increased),
            ResourceConsumed consumed => world.Apply(consumed),
            ResourceProduced produced => world.Apply(produced),
            ResourceAcquired acquired => world.Apply(acquired),
            ResourceLost lost => world.Apply(lost),
            ResourceTransferred transferred => world.Apply(transferred),
            PlayerActionRecorded playerAction => world.Apply(playerAction),
            PlayerCharacterRegistered playerCharacter => world.Apply(playerCharacter),
            WeatherChanged weatherChanged => world.Apply(weatherChanged),
            WorldTimeAdvanced timeAdvanced => world.Apply(timeAdvanced),
            _ => throw new NotSupportedException(
                $"World event '{worldEvent.GetType().Name}' is not supported.")
        };
    }

    public WorldSnapshot Replay(
        WorldSnapshot initialWorld,
        IEnumerable<IWorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(initialWorld);
        ArgumentNullException.ThrowIfNull(events);

        var world = initialWorld;

        foreach (var worldEvent in events)
        {
            world = Apply(world, worldEvent);
        }

        return world;
    }
}
