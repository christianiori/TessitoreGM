using TessitoreGM.Core;

namespace TessitoreGM.AiGm;

public enum AiGmConsequenceImportance
{
    Routine,
    RequiresConfirmation
}

public sealed record AiGmConsequencePolicyOptions(
    int MaximumRoutineCoinAmount = 5,
    int MaximumRoutineResourceQuantity = 1,
    int MaximumRoutineTrustChange = 0);

public sealed record AiGmConsequenceAssessment(
    AiGmConsequenceImportance Importance,
    string Reason);

public sealed class AiGmConsequencePolicy
{
    private readonly AiGmConsequencePolicyOptions _options;
    private readonly IReadOnlySet<EntityId> _humanPlayerIds;

    public AiGmConsequencePolicy(
        IEnumerable<EntityId> humanPlayerIds,
        AiGmConsequencePolicyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(humanPlayerIds);

        _humanPlayerIds = humanPlayerIds.ToHashSet();
        _options = options ?? new AiGmConsequencePolicyOptions();

        if (_options.MaximumRoutineCoinAmount < 0 ||
            _options.MaximumRoutineResourceQuantity < 0 ||
            _options.MaximumRoutineTrustChange < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Routine thresholds cannot be negative.");
        }
    }

    public AiGmConsequenceAssessment Assess(AiGmCommandProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        return proposal switch
        {
            MoveEntityProposal move when IsHuman(move.EntityId) =>
                Important("Lo spostamento riguarda un giocatore umano."),
            MoveEntityProposal => Routine("Movimento ordinario di un PNG."),

            TransferCoinsProposal coins when IsHuman(coins.PayerId) =>
                Important("Un giocatore umano perderebbe monete."),
            TransferCoinsProposal coins
                when coins.Amount > _options.MaximumRoutineCoinAmount =>
                Important("Il trasferimento supera la soglia ordinaria."),
            TransferCoinsProposal => Routine("Trasferimento di piccola entità."),

            AcquireResourceProposal acquired
                when acquired.Quantity > _options.MaximumRoutineResourceQuantity =>
                Important("L'acquisizione supera la soglia ordinaria."),
            AcquireResourceProposal => Routine("Acquisizione di piccola entità."),

            LoseResourceProposal lost when IsHuman(lost.EntityId) =>
                Important("Un giocatore umano perderebbe una risorsa."),
            LoseResourceProposal lost
                when lost.Quantity > _options.MaximumRoutineResourceQuantity =>
                Important("La perdita supera la soglia ordinaria."),
            LoseResourceProposal => Routine("Perdita di piccola entità per un PNG."),

            TransferResourceProposal transfer when IsHuman(transfer.SourceId) =>
                Important("Un giocatore umano cederebbe una risorsa."),
            TransferResourceProposal transfer
                when transfer.Quantity > _options.MaximumRoutineResourceQuantity =>
                Important("Il trasferimento supera la soglia ordinaria."),
            TransferResourceProposal => Routine("Trasferimento di piccola entità."),

            RevealFactProposal =>
                Important("La conoscenza persistente della campagna cambierebbe."),

            ChangeTrustProposal trust
                when Math.Abs((long)trust.Amount) >
                    _options.MaximumRoutineTrustChange =>
                Important("Una relazione persistente cambierebbe."),
            ChangeTrustProposal => Routine("La variazione di fiducia è entro la soglia."),

            _ => Important("Tipo di conseguenza non riconosciuto.")
        };
    }

    private bool IsHuman(EntityId entityId) => _humanPlayerIds.Contains(entityId);

    private static AiGmConsequenceAssessment Routine(string reason) =>
        new(AiGmConsequenceImportance.Routine, reason);

    private static AiGmConsequenceAssessment Important(string reason) =>
        new(AiGmConsequenceImportance.RequiresConfirmation, reason);
}
