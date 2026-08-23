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
        "Il campo authorizedPerspective è l'unica fonte autorizzata per la narrazione al giocatore: ciò che appare soltanto nel catalogo, nella cronaca canonica o nelle memorie altrui resta privato del GM.";

    public const string NewFactDisclosureRule =
        "Puoi narrare un fatto prima ignoto soltanto quando viene comunicato o percepito nel turno corrente; se è un fatto canonico, proponi anche revealFact verso il personaggio giocante.";

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
        NewFactDisclosureRule,
        SceneContinuityRule,
        CatalogRule,
        NarrationStyleRule,
        TypedConsequencesRule
    ];
}
