namespace TessitoreGM.Gm;

public static class UserFacingErrors
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = exception.Message;

        if (Contains(message, "insufficient funds"))
        {
            return "Le monete disponibili non bastano per completare l'operazione.";
        }

        if (Contains(message, "insufficient stock"))
        {
            return "Le risorse disponibili non bastano per completare l'operazione.";
        }

        if (Contains(message, "invalid JSON") ||
            Contains(message, "event log is invalid") ||
            Contains(message, "no initial world state") ||
            Contains(message, "does not contain an events list"))
        {
            return "Il salvataggio è danneggiato o incompleto. Apri Diagnostica per controllare i backup disponibili.";
        }

        if (Contains(message, "repeatedly proposed the same event") ||
            Contains(message, "exceeded the maximum") ||
            Contains(message, "outside simulation interval"))
        {
            return "Una regola della simulazione non riesce a proseguire correttamente. Apri Diagnostica per esaminare l'anteprima delle prossime 24 ore.";
        }

        if (exception is IOException or UnauthorizedAccessException)
        {
            return "Non riesco ad accedere ai file della campagna. Controlla che la cartella sia disponibile e che il file non sia aperto da un altro programma.";
        }

        return RemoveParameterDetails(message);
    }

    private static bool Contains(string message, string value) =>
        message.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string RemoveParameterDetails(string message)
    {
        var parameterMarker = message.IndexOf(
            " (Parameter '",
            StringComparison.Ordinal);
        return parameterMarker < 0
            ? message
            : message[..parameterMarker];
    }
}
