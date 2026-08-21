using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TessitoreGM.Core;

namespace TessitoreGM.Gm;

internal sealed class LanAccessGate
{
    private const string CookieName = "tessitoregm_gm_session";
    private readonly TemporaryAccessCredential _credential = new();

    public LanAccessGate(bool enabled)
    {
        Enabled = enabled;
    }

    public bool Enabled { get; }

    public string AccessCode => _credential.AccessCode;

    public bool IsPublicPath(PathString path) =>
        path.StartsWithSegments("/login") ||
        path.StartsWithSegments("/styles.css") ||
        path.StartsWithSegments("/favicon.ico");

    public bool IsAuthorized(HttpContext context) =>
        !Enabled ||
        (context.Request.Cookies.TryGetValue(
            CookieName,
            out var suppliedToken) &&
         suppliedToken is not null &&
         _credential.VerifySession(suppliedToken));

    public bool TrySignIn(string suppliedCode, HttpResponse response)
    {
        if (!_credential.TryVerifyCode(suppliedCode))
        {
            return false;
        }

        response.Cookies.Append(
            CookieName,
            _credential.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                MaxAge = TimeSpan.FromHours(12)
            });
        return true;
    }

    public static IReadOnlyList<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(network =>
                network.OperationalStatus == OperationalStatus.Up &&
                network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network =>
                network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address =>
                address.AddressFamily == AddressFamily.InterNetwork &&
                IsPrivate(address))
            .Select(address => address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
    }

}
