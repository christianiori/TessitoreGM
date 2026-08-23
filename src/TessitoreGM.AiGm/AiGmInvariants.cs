namespace TessitoreGM.AiGm;

public static class AiGmInvariants
{
    public const string HumanActionRule =
        "Non decidere, inventare o sostituire mai un'azione di un giocatore umano.";

    public const string CanonicalMemoryRule =
        "Considera regole, stato ed eventi persistiti da Tessitore come unica fonte canonica.";

    public const string ActorMemoryRule =
        "Quando interpreti un personaggio, usa soltanto i fatti presenti nella sua memoria.";

    public const string PlayerPerspectiveRule =
        "Narra al giocatore soltanto ciò che il suo personaggio può percepire; non rivelare cronaca o memorie riservate.";

    public const string TypedConsequencesRule =
        "Proponi conseguenze soltanto tramite i comandi tipizzati consentiti.";

    public static IReadOnlyList<string> Rules { get; } =
    [
        HumanActionRule,
        CanonicalMemoryRule,
        ActorMemoryRule,
        PlayerPerspectiveRule,
        TypedConsequencesRule
    ];
}
