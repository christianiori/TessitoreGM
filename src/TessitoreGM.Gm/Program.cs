using TessitoreGM.Gm;

var launchDirectory = Directory.GetCurrentDirectory();
var worldFile = WorldDashboard.ResolveWorldFile(args, launchDirectory);
var actionToken = Guid.NewGuid().ToString("N");
var worldLock = new SemaphoreSlim(1, 1);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content(
    WorldDashboard.Render(worldFile, actionToken),
    "text/html; charset=utf-8"));
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
        WorldDashboard.Advance(worldFile, TimeSpan.FromHours(hours));
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
    worldFile = Path.GetFileName(worldFile),
    worldExists = File.Exists(worldFile)
}));
app.MapGet("/favicon.ico", () => Results.NoContent());

Console.WriteLine("TessitoreGM — Tavolo del GM");
Console.WriteLine($"Mondo: {worldFile}");
Console.WriteLine("Apri sul PC: http://localhost:5074");
app.Run("http://127.0.0.1:5074");
