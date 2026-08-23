using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.World;

namespace TessitoreGM.AiGm;

public sealed record AiGmProposalValidation(bool IsValid, string? Error)
{
    public static AiGmProposalValidation Valid { get; } = new(true, null);

    public static AiGmProposalValidation Invalid(string error) =>
        new(false, error);
}

public sealed class AiGmProposalValidator
{
    private readonly WorldSnapshot _world;
    private readonly IReadOnlySet<EntityId> _entities;
    private readonly IReadOnlySet<LocationId> _locations;
    private readonly IReadOnlySet<ResourceId> _resources;

    public AiGmProposalValidator(WorldEventLog eventLog, WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
        _entities = KnownEntities(eventLog);
        _locations = KnownLocations(eventLog);
        _resources = KnownResources(eventLog);
    }

    public AiGmProposalValidation Validate(AiGmCommandProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (string.IsNullOrWhiteSpace(proposal.Reason))
        {
            return AiGmProposalValidation.Invalid(
                "La conseguenza deve avere una motivazione.");
        }

        return proposal switch
        {
            MoveEntityProposal move => ValidateMove(move),
            TransferCoinsProposal coins => ValidateCoins(coins),
            AcquireResourceProposal acquired => ValidateAcquire(acquired),
            LoseResourceProposal lost => ValidateLoss(lost),
            TransferResourceProposal transfer => ValidateTransfer(transfer),
            RevealFactProposal revealed => ValidateReveal(revealed),
            ChangeTrustProposal trust => ValidateTrust(trust),
            _ => AiGmProposalValidation.Invalid(
                "Il tipo di conseguenza non è consentito.")
        };
    }

    private AiGmProposalValidation ValidateMove(MoveEntityProposal proposal)
    {
        var entity = RequireEntity(proposal.EntityId);
        if (!entity.IsValid)
        {
            return entity;
        }

        if (!_locations.Contains(proposal.DestinationId))
        {
            return AiGmProposalValidation.Invalid(
                $"Il luogo '{proposal.DestinationId}' non esiste.");
        }

        return _world.GetLocation(proposal.EntityId) == proposal.DestinationId
            ? AiGmProposalValidation.Invalid("L'entità è già nel luogo indicato.")
            : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateCoins(TransferCoinsProposal proposal)
    {
        var participants = RequireTwoEntities(proposal.PayerId, proposal.PayeeId);
        if (!participants.IsValid)
        {
            return participants;
        }

        if (proposal.Amount <= 0)
        {
            return AiGmProposalValidation.Invalid(
                "La quantità di monete deve essere positiva.");
        }

        return _world.GetBalance(proposal.PayerId) < proposal.Amount
            ? AiGmProposalValidation.Invalid("Il pagatore non ha abbastanza monete.")
            : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateAcquire(AcquireResourceProposal proposal)
    {
        var entity = RequireEntity(proposal.EntityId);
        if (!entity.IsValid)
        {
            return entity;
        }

        return ValidateResourceAndQuantity(proposal.ResourceId, proposal.Quantity);
    }

    private AiGmProposalValidation ValidateLoss(LoseResourceProposal proposal)
    {
        var entity = RequireEntity(proposal.EntityId);
        if (!entity.IsValid)
        {
            return entity;
        }

        var resource = ValidateResourceAndQuantity(proposal.ResourceId, proposal.Quantity);
        if (!resource.IsValid)
        {
            return resource;
        }

        return _world.GetResourceQuantity(proposal.EntityId, proposal.ResourceId) <
            proposal.Quantity
                ? AiGmProposalValidation.Invalid(
                    "L'entità non possiede abbastanza unità della risorsa.")
                : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateTransfer(
        TransferResourceProposal proposal)
    {
        var participants = RequireTwoEntities(
            proposal.SourceId,
            proposal.DestinationId);
        if (!participants.IsValid)
        {
            return participants;
        }

        var resource = ValidateResourceAndQuantity(proposal.ResourceId, proposal.Quantity);
        if (!resource.IsValid)
        {
            return resource;
        }

        return _world.GetResourceQuantity(proposal.SourceId, proposal.ResourceId) <
            proposal.Quantity
                ? AiGmProposalValidation.Invalid(
                    "La sorgente non possiede abbastanza unità della risorsa.")
                : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateReveal(RevealFactProposal proposal)
    {
        var entity = RequireEntity(proposal.EntityId);
        if (!entity.IsValid)
        {
            return entity;
        }

        return string.IsNullOrWhiteSpace(proposal.FactId.Value)
            ? AiGmProposalValidation.Invalid("Il fatto non può essere vuoto.")
            : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateTrust(ChangeTrustProposal proposal)
    {
        var participants = RequireTwoEntities(
            proposal.SubjectId,
            proposal.OtherEntityId);
        if (!participants.IsValid)
        {
            return participants;
        }

        return proposal.Amount == 0
            ? AiGmProposalValidation.Invalid(
                "La variazione di fiducia non può essere zero.")
            : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation RequireEntity(EntityId entityId) =>
        _entities.Contains(entityId)
            ? AiGmProposalValidation.Valid
            : AiGmProposalValidation.Invalid(
                $"L'entità '{entityId}' non esiste.");

    private AiGmProposalValidation RequireTwoEntities(
        EntityId first,
        EntityId second)
    {
        var firstResult = RequireEntity(first);
        if (!firstResult.IsValid)
        {
            return firstResult;
        }

        var secondResult = RequireEntity(second);
        if (!secondResult.IsValid)
        {
            return secondResult;
        }

        return first == second
            ? AiGmProposalValidation.Invalid(
                "Le due entità devono essere diverse.")
            : AiGmProposalValidation.Valid;
    }

    private AiGmProposalValidation ValidateResourceAndQuantity(
        ResourceId resourceId,
        int quantity)
    {
        if (!_resources.Contains(resourceId))
        {
            return AiGmProposalValidation.Invalid(
                $"La risorsa '{resourceId}' non esiste.");
        }

        return quantity <= 0
            ? AiGmProposalValidation.Invalid(
                "La quantità della risorsa deve essere positiva.")
            : AiGmProposalValidation.Valid;
    }

    private static IReadOnlySet<EntityId> KnownEntities(WorldEventLog eventLog) =>
        eventLog.InitialWorld.Balances.Select(balance => balance.EntityId)
            .Concat(eventLog.Events.OfType<PlayerCharacterRegistered>()
                .Select(player => player.EntityId))
            .Concat(eventLog.Simulation?.Npcs.Select(npc => npc.EntityId) ?? [])
            .Concat(eventLog.Simulation?.Entities?.Select(entity => entity.EntityId) ?? [])
            .ToHashSet();

    private static IReadOnlySet<LocationId> KnownLocations(WorldEventLog eventLog) =>
        (eventLog.Simulation?.Locations?.Select(location => location.LocationId) ?? [])
            .Concat(eventLog.Events.OfType<EntityEnteredLocation>()
                .Select(entered => entered.LocationId))
            .ToHashSet();

    private static IReadOnlySet<ResourceId> KnownResources(WorldEventLog eventLog) =>
        (eventLog.Simulation?.Resources?.Select(resource => resource.ResourceId) ?? [])
            .Concat((eventLog.InitialWorld.ResourceStocks ?? [])
                .Select(stock => stock.ResourceId))
            .ToHashSet();
}
