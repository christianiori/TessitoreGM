using TessitoreGM.Core;

namespace TessitoreGM.Gm;

internal sealed class PlayerAccessGate
{
    private const string CookieName = "tessitoregm_player_session";
    private readonly TemporaryPlayerAccessRegistry _registry = new();

    public bool IsPublicPath(PathString path) =>
        path.StartsWithSegments("/player-login");

    public string IssueCode(string entityId) =>
        _registry.IssueCode(new EntityId(entityId));

    public bool TrySignIn(
        string suppliedCode,
        HttpResponse response,
        out EntityId entityId)
    {
        if (!_registry.TryExchangeCode(
                suppliedCode,
                out entityId,
                out var sessionToken))
        {
            return false;
        }

        response.Cookies.Append(
            CookieName,
            sessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                MaxAge = TimeSpan.FromHours(12),
                Path = "/"
            });
        return true;
    }

    public bool IsAuthorizedRequest(HttpContext context)
    {
        if (!TryGetRequestedEntity(context.Request.Path, out var entityId) ||
            !context.Request.Cookies.TryGetValue(
                CookieName,
                out var suppliedToken) ||
            suppliedToken is null)
        {
            return false;
        }

        return _registry.VerifySession(entityId, suppliedToken);
    }

    public bool TryGetAuthorizedEntity(
        HttpContext context,
        out EntityId entityId)
    {
        if (context.Request.Cookies.TryGetValue(
                CookieName,
                out var suppliedToken) &&
            suppliedToken is not null)
        {
            return _registry.TryResolveSession(suppliedToken, out entityId);
        }

        entityId = default;
        return false;
    }

    public void RevokeAll() => _registry.RevokeAll();

    private static bool TryGetRequestedEntity(
        PathString path,
        out EntityId entityId)
    {
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length >= 2 &&
            segments[0].Equals("player", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                entityId = new EntityId(Uri.UnescapeDataString(segments[1]));
                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        entityId = default;
        return false;
    }
}
