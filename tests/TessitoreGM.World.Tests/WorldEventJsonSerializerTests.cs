using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldEventJsonSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_WorldAndEvents_PreservesLog()
    {
        var eventLog = CreateEventLog();
        var serializer = new WorldEventJsonSerializer();

        var json = serializer.Serialize(eventLog);
        var restoredLog = serializer.Deserialize(json);

        Assert.Equal(
            eventLog.InitialWorld.CurrentTime,
            restoredLog.InitialWorld.CurrentTime);
        Assert.Equal(
            eventLog.InitialWorld.Balances,
            restoredLog.InitialWorld.Balances);
        Assert.Equal(eventLog.Simulation, restoredLog.Simulation);
        Assert.Equal(eventLog.Events.Count, restoredLog.Events.Count);

        for (var index = 0; index < eventLog.Events.Count; index++)
        {
            Assert.Equal(
                eventLog.Events[index].GetType(),
                restoredLog.Events[index].GetType());
            Assert.Equal(eventLog.Events[index], restoredLog.Events[index]);
        }
    }

    [Fact]
    public void DeserializeThenReplay_SelfContainedLog_ReconstructsWorld()
    {
        var serializer = new WorldEventJsonSerializer();
        var restoredLog = serializer.Deserialize(
            serializer.Serialize(CreateEventLog("hammer", 14, 4)));
        var balances = restoredLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            restoredLog.InitialWorld.CurrentTime,
            balances);

        var finalWorld = new WorldEventProcessor().Replay(
            initialWorld,
            restoredLog.Events);

        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId("hammer-1");
        Assert.Equal(OrderStatus.Delivered, finalWorld.GetOrder(orderId)?.Status);
        Assert.Equal(14, finalWorld.GetOrder(orderId)?.AmountPaid);
        Assert.Equal(0, finalWorld.GetBalance(customerId));
        Assert.Equal(39, finalWorld.GetBalance(blacksmithId));
        Assert.Equal("hammer", finalWorld.GetItem(itemId)?.Name);
        Assert.Equal(customerId, finalWorld.GetItem(itemId)?.OwnerId);
        Assert.Equal(At(4, 9, 3), finalWorld.CurrentTime);
    }

    [Fact]
    public void Deserialize_OldVersion_Throws()
    {
        var serializer = new WorldEventJsonSerializer();
        var json = serializer.Serialize(CreateEventLog())
            .Replace("\"version\": 2", "\"version\": 1");

        var exception = Assert.Throws<InvalidDataException>(
            () => serializer.Deserialize(json));

        Assert.Contains("version '1'", exception.Message);
    }

    [Fact]
    public void SerializeThenDeserialize_WorldTimeAdvanced_PreservesEvent()
    {
        var initialTime = At(3, 8, 0);
        var timeAdvanced = new WorldTimeAdvanced(At(3, 13, 0));
        var eventLog = new WorldEventLog(
            new WorldInitialState(initialTime, Array.Empty<EntityBalance>()),
            new IWorldEvent[] { timeAdvanced });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(eventLog));
        var restoredWorld = Replay(restored);

        Assert.Equal(
            timeAdvanced,
            Assert.IsType<WorldTimeAdvanced>(Assert.Single(restored.Events)));
        Assert.Equal(timeAdvanced.OccurredAt, restoredWorld.CurrentTime);
    }

    [Fact]
    public void SerializeThenDeserialize_SimulationDefinition_PreservesPlan()
    {
        var npcId = new EntityId("artisan");
        var locationId = new LocationId("workshop");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId("shield-1");
        var simulation = new WorldSimulationDefinition(
            new NpcSimulationDefinition[]
            {
                new(
                    npcId,
                    "armorer",
                    new ScheduledArrivalDefinition[]
                    {
                        new(locationId, At(3, 9, 0))
                    },
                    new OrderProductionDefinition[]
                    {
                        new(
                            orderId,
                            itemId,
                            locationId,
                            At(3, 9, 30),
                            TimeSpan.FromHours(2))
                    },
                    new DailyLocationRoutineDefinition[]
                    {
                        new(locationId, TimeSpan.FromHours(9))
                    },
                    new BalanceConditionalLocationDefinition[]
                    {
                        new(locationId, TimeSpan.FromHours(12), 5)
                    })
            },
            new EntityPresentationDefinition[]
            {
                new(npcId, "Doran the armorer")
            },
            new LocationPresentationDefinition[]
            {
                new(locationId, "Doran's workshop")
            });
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(3, 8, 0),
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            simulation);

        var restored = RoundTrip(eventLog);

        var restoredSimulation = Assert.IsType<WorldSimulationDefinition>(
            restored.Simulation);
        var restoredNpc = Assert.Single(restoredSimulation.Npcs);
        Assert.Equal(npcId, restoredNpc.EntityId);
        Assert.Equal("armorer", restoredNpc.Role);
        Assert.Equal(
            Assert.Single(simulation.Npcs).ScheduledArrivals,
            restoredNpc.ScheduledArrivals);
        Assert.Equal(
            Assert.Single(simulation.Npcs).OrderProductions,
            restoredNpc.OrderProductions);
        Assert.Equal(
            Assert.Single(simulation.Npcs).DailyLocationRoutines,
            restoredNpc.DailyLocationRoutines);
        Assert.Equal(
            Assert.Single(simulation.Npcs).BalanceConditionalLocations,
            restoredNpc.BalanceConditionalLocations);
        Assert.Equal(simulation.Entities, restoredSimulation.Entities);
        Assert.Equal(simulation.Locations, restoredSimulation.Locations);
    }

    [Fact]
    public void SaveReloadAndAppend_ProgressesWorldAcrossThreeSessions()
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId("hammer-1");
        var eventLog = new WorldEventLog(
            new WorldInitialState(
                At(3, 8, 0),
                new EntityBalance[]
                {
                    new(customerId, 14),
                    new(blacksmithId, 25)
                }),
            new IWorldEvent[]
            {
                new EntityEnteredLocation(customerId, forgeId, At(3, 8, 10)),
                new OrderRequested(
                    orderId,
                    customerId,
                    blacksmithId,
                    "hammer",
                    At(3, 8, 11)),
                new OrderAccepted(orderId, 14, At(3, 8, 12)),
                new PaymentTransferred(
                    orderId,
                    customerId,
                    blacksmithId,
                    4,
                    At(3, 8, 13)),
                new EntityLeftLocation(customerId, forgeId, At(3, 8, 14))
            });

        eventLog = RoundTrip(eventLog);
        var acceptedWorld = Replay(eventLog);
        Assert.Equal(OrderStatus.Accepted, acceptedWorld.GetOrder(orderId)?.Status);
        Assert.Equal(5, eventLog.Events.Count);

        eventLog = eventLog with
        {
            Events = eventLog.Events.Concat(new IWorldEvent[]
            {
                new OrderWorkStarted(orderId, At(3, 8, 30)),
                new OrderCompleted(orderId, itemId, At(3, 12, 30))
            }).ToArray()
        };
        eventLog = RoundTrip(eventLog);
        var completedWorld = Replay(eventLog);
        Assert.Equal(OrderStatus.Completed, completedWorld.GetOrder(orderId)?.Status);
        Assert.Equal(blacksmithId, completedWorld.GetItem(itemId)?.OwnerId);
        Assert.Equal(7, eventLog.Events.Count);

        eventLog = eventLog with
        {
            Events = eventLog.Events.Concat(new IWorldEvent[]
            {
                new EntityEnteredLocation(customerId, forgeId, At(4, 9, 0)),
                new PaymentTransferred(
                    orderId,
                    customerId,
                    blacksmithId,
                    10,
                    At(4, 9, 1)),
                new OrderDelivered(orderId, At(4, 9, 2)),
                new EntityLeftLocation(customerId, forgeId, At(4, 9, 3))
            }).ToArray()
        };
        eventLog = RoundTrip(eventLog);
        var deliveredWorld = Replay(eventLog);
        Assert.Equal(OrderStatus.Delivered, deliveredWorld.GetOrder(orderId)?.Status);
        Assert.Equal(customerId, deliveredWorld.GetItem(itemId)?.OwnerId);
        Assert.Equal(0, deliveredWorld.GetBalance(customerId));
        Assert.Equal(39, deliveredWorld.GetBalance(blacksmithId));
        Assert.Equal(11, eventLog.Events.Count);
    }

    private static WorldEventLog RoundTrip(WorldEventLog eventLog)
    {
        var serializer = new WorldEventJsonSerializer();
        return serializer.Deserialize(serializer.Serialize(eventLog));
    }

    private static WorldSnapshot Replay(WorldEventLog eventLog)
    {
        var balances = eventLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            balances);

        return new WorldEventProcessor().Replay(initialWorld, eventLog.Events);
    }

    private static WorldEventLog CreateEventLog(
        string itemName = "sword",
        int totalPrice = 10,
        int deposit = 5)
    {
        var customerId = new EntityId("customer");
        var blacksmithId = new EntityId("blacksmith");
        var forgeId = new LocationId("forge");
        var orderId = new OrderId("order-1");
        var itemId = new ItemId($"{itemName}-1");
        IWorldEvent[] events =
        {
            new EntityEnteredLocation(customerId, forgeId, At(3, 8, 10)),
            new OrderRequested(
                orderId,
                customerId,
                blacksmithId,
                itemName,
                At(3, 8, 11)),
            new OrderAccepted(orderId, totalPrice, At(3, 8, 12)),
            new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                deposit,
                At(3, 8, 13)),
            new EntityLeftLocation(customerId, forgeId, At(3, 8, 14)),
            new OrderWorkStarted(orderId, At(3, 8, 30)),
            new OrderCompleted(orderId, itemId, At(3, 12, 30)),
            new EntityEnteredLocation(customerId, forgeId, At(4, 9, 0)),
            new PaymentTransferred(
                orderId,
                customerId,
                blacksmithId,
                totalPrice - deposit,
                At(4, 9, 1)),
            new OrderDelivered(orderId, At(4, 9, 2)),
            new EntityLeftLocation(customerId, forgeId, At(4, 9, 3))
        };

        return new WorldEventLog(
            new WorldInitialState(
                At(3, 8, 0),
                new EntityBalance[]
                {
                    new(customerId, totalPrice),
                    new(blacksmithId, 25)
                }),
            events);
    }

    private static DateTimeOffset At(int day, int hour, int minute) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);
}
