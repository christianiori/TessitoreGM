using TessitoreGM.Gm;

namespace TessitoreGM.World.Tests;

public sealed class UserFacingErrorsTests
{
    [Fact]
    public void Describe_InsufficientFunds_UsesPlainItalian()
    {
        var message = UserFacingErrors.Describe(
            new InvalidOperationException("Entity 'hero' has insufficient funds."));

        Assert.Equal(
            "Le monete disponibili non bastano per completare l'operazione.",
            message);
    }

    [Fact]
    public void Describe_InvalidSave_DirectsUserToDiagnostics()
    {
        var message = UserFacingErrors.Describe(
            new InvalidDataException("The event log contains invalid JSON."));

        Assert.Contains("salvataggio", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Diagnostica", message);
        Assert.DoesNotContain("JSON", message);
    }

    [Fact]
    public void Describe_ArgumentError_RemovesTechnicalParameterSuffix()
    {
        var message = UserFacingErrors.Describe(
            new ArgumentException("Personaggio non valido.", "entityId"));

        Assert.Equal("Personaggio non valido.", message);
    }

    [Fact]
    public void Describe_IoError_ExplainsWhatUserCanCheck()
    {
        var message = UserFacingErrors.Describe(
            new IOException("The process cannot access the file."));

        Assert.Contains("cartella", message);
        Assert.Contains("altro programma", message);
    }
}
