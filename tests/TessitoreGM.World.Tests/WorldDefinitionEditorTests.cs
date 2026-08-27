using TessitoreGM.Core;
using TessitoreGM.Events;

namespace TessitoreGM.World.Tests;

public sealed class WorldDefinitionEditorTests
{
    [Fact]
    public void AddLocation_PreservesExistingWorldAndAddsPresentation()
    {
        var existing = new LocationPresentationDefinition(
            new LocationId("market"),
            "Mercato");
        var log = CreateLog(new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>(),
            Locations: new[] { existing }));

        var updated = new WorldDefinitionEditor().AddLocation(
            log,
            "  north-tower ",
            " Torre Nord ");

        Assert.Equal(log.Events, updated.Events);
        Assert.Equal(log.InitialWorld, updated.InitialWorld);
        Assert.Collection(
            updated.Simulation!.Locations!,
            location => Assert.Equal(existing, location),
            location =>
            {
                Assert.Equal(new LocationId("north-tower"), location.LocationId);
                Assert.Equal("Torre Nord", location.Name);
            });
    }

    [Fact]
    public void AddResource_WithoutSimulation_CreatesMinimalDefinition()
    {
        var log = CreateLog();

        var updated = new WorldDefinitionEditor().AddResource(
            log,
            "wood",
            "Legname");

        Assert.NotNull(updated.Simulation);
        Assert.Empty(updated.Simulation!.Npcs);
        var resource = Assert.Single(updated.Simulation.Resources!);
        Assert.Equal(new ResourceId("wood"), resource.ResourceId);
        Assert.Equal("Legname", resource.Name);
    }

    [Fact]
    public void AddLocation_DuplicateNameOrId_IsRejected()
    {
        var log = CreateLog(new WorldSimulationDefinition(
            Array.Empty<NpcSimulationDefinition>(),
            Locations: new[]
            {
                new LocationPresentationDefinition(
                    new LocationId("market"),
                    "Mercato")
            }));
        var editor = new WorldDefinitionEditor();

        Assert.Throws<InvalidOperationException>(() =>
            editor.AddLocation(log, "MARKET", "Altro mercato"));
        Assert.Throws<InvalidOperationException>(() =>
            editor.AddLocation(log, "other-market", "mercato"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("spazio vietato")]
    [InlineData("città")]
    [InlineData("path/segment")]
    public void AddResource_InvalidIdentifier_IsRejected(string id)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new WorldDefinitionEditor().AddResource(
                CreateLog(),
                id,
                "Risorsa"));

        Assert.Contains("identificatore", exception.Message);
    }

    private static WorldEventLog CreateLog(
        WorldSimulationDefinition? simulation = null) => new(
            new WorldInitialState(
                new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero),
                Array.Empty<EntityBalance>()),
            Array.Empty<IWorldEvent>(),
            simulation);
}
