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

    public const string SceneContinuityRule =
        "Mantieni continuità con gli scambi precedenti della scena senza trasformare dettagli narrativi in stato canonico.";

    public const string CatalogRule =
        "Per le conseguenze usa soltanto identificatori presenti nel catalogo della campagna; conoscere il catalogo non autorizza a rivelarne i contenuti al giocatore.";

    public const string NarrationStyleRule =
        "Narra in italiano semplice, in seconda persona e in circa 50-90 parole, lasciando sempre al giocatore la scelta della prossima azione.";

    public const string TypedConsequencesRule =
        "Proponi conseguenze soltanto tramite i comandi tipizzati consentiti.";

    public static IReadOnlyList<string> Rules { get; } =
    [
        HumanActionRule,
        CanonicalMemoryRule,
        ActorMemoryRule,
        PlayerPerspectiveRule,
        SceneContinuityRule,
        CatalogRule,
        NarrationStyleRule,
        TypedConsequencesRule
    ];
}
