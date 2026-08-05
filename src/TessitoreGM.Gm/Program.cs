using TessitoreGM.Gm;

var launchDirectory = Directory.GetCurrentDirectory();
var lanEnabled = args.Any(argument =>
    argument.Equals("--lan", StringComparison.OrdinalIgnoreCase));
var portArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--port=", StringComparison.OrdinalIgnoreCase));
var port = portArgument is null
    ? 5074
    : int.TryParse(portArgument[7..], out var suppliedPort) &&
      suppliedPort is >= 1024 and <= 65535
        ? suppliedPort
        : throw new ArgumentException(
            "La porta deve essere un numero compreso tra 1024 e 65535.");
var accessGate = new LanAccessGate(lanEnabled);
var activeWorldFile = WorldDashboard.ResolveWorldFile(args, launchDirectory);
var campaignCatalog = new CampaignCatalog(
    Path.GetDirectoryName(activeWorldFile) ?? launchDirectory);
var actionToken = Guid.NewGuid().ToString("N");
var worldLock = new SemaphoreSlim(1, 1);
PendingWorldAdvance? pendingAdvance = null;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (accessGate.IsPublicPath(context.Request.Path) ||
        accessGate.IsAuthorized(context))
    {
        await next();
        return;
    }

    if (HttpMethods.IsGet(context.Request.Method))
    {
        context.Response.Redirect("/login");
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

app.MapGet("/login", () => Results.Content(
    WorldDashboard.RenderLogin(error: false),
    "text/html; charset=utf-8"));
app.MapPost("/login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (accessGate.TrySignIn(
        form["accessCode"].ToString(),
        context.Response))
    {
        context.Response.Redirect("/");
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(
        WorldDashboard.RenderLogin(error: true));
});

app.MapGet("/", () => Results.Content(
    WorldDashboard.Render(
        activeWorldFile,
        actionToken,
        campaignCatalog.Discover(),
        pendingAdvance),
    "text/html; charset=utf-8"));
app.MapGet("/chronicle", () => Results.Content(
    WorldDashboard.RenderChronicle(activeWorldFile),
    "text/html; charset=utf-8"));
app.MapPost("/campaign/select", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        activeWorldFile = campaignCatalog.Select(
            form["campaign"].ToString());
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/campaign/create", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        var templatePath = File.Exists(activeWorldFile)
            ? activeWorldFile
            : campaignCatalog.Discover().Select(entry =>
                campaignCatalog.Select(entry.FileName)).FirstOrDefault()
                ?? throw new ArgumentException(
                    "Non esiste ancora un modello da cui creare il mondo.");
        activeWorldFile = campaignCatalog.Create(
            form["name"].ToString(),
            templatePath);
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/advance", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken ||
        !int.TryParse(form["hours"], out var hours) ||
        hours is not (1 or 6 or 24))
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        pendingAdvance = WorldDashboard.PreviewAdvance(
            activeWorldFile,
            TimeSpan.FromHours(hours));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/advance/approve", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken || pendingAdvance is null)
    {
        return Results.BadRequest("Non esiste un'anteprima da approvare.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.ApproveAdvance(activeWorldFile, pendingAdvance);
        pendingAdvance = null;
    }
    catch (InvalidOperationException exception)
    {
        pendingAdvance = null;
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/advance/reject", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        pendingAdvance = null;
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/move", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.MoveEntity(
            activeWorldFile,
            form["entity"].ToString(),
            form["location"].ToString());
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/reveal", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.RevealFact(
            activeWorldFile,
            form["entity"].ToString(),
            form["fact"].ToString(),
            form["newFact"].ToString());
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/player-action", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.RecordPlayerAction(
            activeWorldFile,
            form["actor"].ToString(),
            form["description"].ToString());
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/player-character", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.RegisterPlayerCharacter(
            activeWorldFile,
            form["name"].ToString());
        pendingAdvance = null;
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/coins/transfer", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken ||
        !int.TryParse(form["amount"], out var amount))
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.TransferCoins(
            activeWorldFile,
            form["payer"].ToString(),
            form["payee"].ToString(),
            amount,
            form["reason"].ToString());
        pendingAdvance = null;
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/resources/change", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken ||
        !int.TryParse(form["quantity"], out var quantity))
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.ApplyResourceConsequence(
            activeWorldFile,
            form["operation"].ToString(),
            form["entity"].ToString(),
            form["destination"].ToString(),
            form["resource"].ToString(),
            quantity,
            form["reason"].ToString());
        pendingAdvance = null;
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(exception.Message);
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapGet("/styles.css", () => Results.Text(
    DashboardStyles.Content,
    "text/css; charset=utf-8"));
app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    worldFile = Path.GetFileName(activeWorldFile),
    worldExists = File.Exists(activeWorldFile)
}));
app.MapGet("/favicon.ico", () => Results.NoContent());

Console.WriteLine("TessitoreGM — Tavolo del GM");
Console.WriteLine($"Mondo iniziale: {activeWorldFile}");
Console.WriteLine($"Apri sul PC: http://localhost:{port}");
if (lanEnabled)
{
    Console.WriteLine($"Codice di accesso: {accessGate.AccessCode}");
    foreach (var address in LanAccessGate.LocalAddresses())
    {
        Console.WriteLine($"Apri sul telefono: http://{address}:{port}");
    }
}
app.Run(lanEnabled
    ? $"http://0.0.0.0:{port}"
    : $"http://127.0.0.1:{port}");
