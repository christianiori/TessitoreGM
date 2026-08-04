using TessitoreGM.Gm;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var worldFile = WorldDashboard.ResolveWorldFile(args);

app.MapGet("/", () => Results.Content(
    WorldDashboard.Render(worldFile),
    "text/html; charset=utf-8"));
app.MapGet("/styles.css", () => Results.Text(
    DashboardStyles.Content,
    "text/css; charset=utf-8"));
app.MapGet("/health", () => Results.Json(new
{
    status = "ok",
    worldFile = Path.GetFileName(worldFile),
    worldExists = File.Exists(worldFile)
}));

Console.WriteLine("TessitoreGM — Tavolo del GM");
Console.WriteLine($"Mondo: {worldFile}");
Console.WriteLine("Apri sul PC: http://localhost:5074");
app.Run("http://127.0.0.1:5074");
