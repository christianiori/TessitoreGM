using TessitoreGM.Core;

namespace TessitoreGM.World.Tests;

public sealed class TemporaryAccessCredentialTests
{
    [Fact]
    public void GeneratedCredential_AcceptsCodeAndSessionToken()
    {
        var credential = new TemporaryAccessCredential();

        Assert.Equal(8, credential.AccessCode.Length);
        Assert.True(credential.AccessCode.All(char.IsDigit));
        Assert.True(credential.TryVerifyCode(credential.AccessCode));
        Assert.True(credential.VerifySession(credential.SessionToken));
        Assert.False(credential.VerifySession("wrong-session"));
    }

    [Fact]
    public void TryVerifyCode_AfterFiveFailures_TemporarilyRejectsCorrectCode()
    {
        var credential = new TemporaryAccessCredential();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False(credential.TryVerifyCode("00000000"));
        }

        Assert.False(credential.TryVerifyCode(credential.AccessCode));
    }
}
