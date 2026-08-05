using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class PlayerActionQueueTests
{
    [Fact]
    public void SerializeThenDeserialize_ActionWithD20Result_PreservesWorkflow()
    {
        var at = new DateTimeOffset(
            2026, 8, 3, 14, 0, 0, TimeSpan.Zero);
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            new EntityId("pc:arianna"),
            "Cerco tracce vicino al granaio.",
            at,
            PlayerActionStatus.Rolled,
            Roll: new D20Roll(
                3,
                15,
                false,
                D20RollMode.Advantage,
                at,
                new[] { 7, 16 },
                16,
                19,
                at));
        var log = new WorldEventLog(
            new WorldInitialState(
                at,
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            PlayerActions: new[] { action });
        var serializer = new WorldEventJsonSerializer();

        var restored = serializer.Deserialize(serializer.Serialize(log));

        var restoredAction = Assert.Single(restored.PlayerActions!);
        Assert.Equal(action.Id, restoredAction.Id);
        Assert.Equal(action.Description, restoredAction.Description);
        Assert.Equal(PlayerActionStatus.Rolled, restoredAction.Status);
        Assert.Equal(new[] { 7, 16 }, restoredAction.Roll?.Dice);
        Assert.Equal(19, restoredAction.Roll?.Total);
    }

    [Fact]
    public void Serialize_DuplicateActionIds_RejectsQueue()
    {
        var at = new DateTimeOffset(
            2026, 8, 3, 14, 0, 0, TimeSpan.Zero);
        var action = new PlayerActionProposal(
            Guid.NewGuid(),
            new EntityId("pc:arianna"),
            "Osservo la porta.",
            at);
        var serializer = new WorldEventJsonSerializer();
        var log = new WorldEventLog(
            new WorldInitialState(
                at,
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            PlayerActions: new[] { action, action });

        Assert.Throws<InvalidDataException>(() => serializer.Serialize(log));
    }
}
