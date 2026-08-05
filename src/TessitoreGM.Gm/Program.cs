using TessitoreGM.Gm;

var launchDirectory = Directory.GetCurrentDirectory();
var activeWorldFile = WorldDashboard.ResolveWorldFile(args, launchDirectory);
var campaignCatalog = new CampaignCatalog(
    Path.GetDirectoryName(activeWorldFile) ?? launchDirectory);
var actionToken = Guid.NewGuid().ToString("N");
var worldLock = new SemaphoreSlim(1, 1);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(
    WorldDashboard.Render(
        activeWorldFile,
        actionToken,
        campaignCatalog.Discover()),
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
        WorldDashboard.Advance(activeWorldFile, TimeSpan.FromHours(hours));
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
Console.WriteLine("Apri sul PC: http://localhost:5074");
app.Run("http://127.0.0.1:5074");
