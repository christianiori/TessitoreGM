using TessitoreGM.Core;

namespace TessitoreGM.AiGm;

/// <summary>
/// Closed set of world consequences an AI GM may propose. There is
/// intentionally no proposal representing a human player's action.
/// </summary>
public abstract record AiGmCommandProposal(string Reason);

public sealed record MoveEntityProposal(
    EntityId EntityId,
    LocationId DestinationId,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record TransferCoinsProposal(
    EntityId PayerId,
    EntityId PayeeId,
    int Amount,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record AcquireResourceProposal(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record LoseResourceProposal(
    EntityId EntityId,
    ResourceId ResourceId,
    int Quantity,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record TransferResourceProposal(
    EntityId SourceId,
    EntityId DestinationId,
    ResourceId ResourceId,
    int Quantity,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record RevealFactProposal(
    EntityId EntityId,
    FactId FactId,
    string Reason) : AiGmCommandProposal(Reason);

public sealed record ChangeTrustProposal(
    EntityId SubjectId,
    EntityId OtherEntityId,
    int Amount,
    string Reason) : AiGmCommandProposal(Reason);
