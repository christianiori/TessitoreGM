using System.Collections.Concurrent;
using System.Diagnostics;
using TessitoreGM.AiGm;
using TessitoreGM.Gm;
using TessitoreGM.World;

var launchDirectory = Directory.GetCurrentDirectory();
var explicitWorldFile = args.Any(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
var lanEnabled = args.Any(argument =>
    argument.Equals("--lan", StringComparison.OrdinalIgnoreCase));
var browserEnabled = !explicitWorldFile && !args.Any(argument =>
    argument.Equals("--no-browser", StringComparison.OrdinalIgnoreCase));
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
var playerAccessGate = new PlayerAccessGate();
var activeWorldFile = explicitWorldFile
    ? WorldDashboard.ResolveWorldFile(args, launchDirectory)
    : StandaloneWorkspace.PrepareDefaultCampaign(
        launchDirectory,
        AppContext.BaseDirectory);
var campaignCatalog = new CampaignCatalog(
    Path.GetDirectoryName(activeWorldFile) ?? launchDirectory);
var pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
var loadedPlugins = new WorldPluginLoader().Load(pluginDirectory);
var aiGmSettingsStore = new AiGmModeSettingsStore(
    StandaloneWorkspace.ResolveAiGmSettingsFile(activeWorldFile));
var ollamaHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(2)
};
var actionToken = Guid.NewGuid().ToString("N");
var playerInteractionToken = Guid.NewGuid().ToString("N");
var worldLock = new SemaphoreSlim(1, 1);
var aiGmActionsInProgress = new ConcurrentDictionary<Guid, byte>();
PendingWorldAdvance? pendingAdvance = null;
string? focusedScene = null;
string? aiGmRuntimeNotice = null;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

async Task RunConfiguredAiGmAsync(
    string worldFile,
    TessitoreGM.Events.PlayerActionProposal proposal,
    CancellationToken cancellationToken)
{
    void SetNotice(string notice)
    {
        if (Path.GetFullPath(activeWorldFile).Equals(
            Path.GetFullPath(worldFile),
            StringComparison.OrdinalIgnoreCase))
        {
            aiGmRuntimeNotice = notice;
        }
    }

    var settings = aiGmSettingsStore.Get(worldFile);
    if (!settings.Enabled)
    {
        SetNotice(
            "La modalità AI non è attiva. L'azione resta al GM umano.");
        return;
    }
    if (!settings.ProviderConfigured)
    {
        SetNotice("Configura Ollama prima di affidargli le azioni.");
        return;
    }
    if (!settings.ProviderId!.Equals(
        "ollama",
        StringComparison.OrdinalIgnoreCase))
    {
        SetNotice(
            $"Il fornitore '{settings.ProviderId}' non è ancora disponibile. " +
            "L'azione resta al GM umano.");
        return;
    }

    try
    {
        var eventStore = new TessitoreGM.Events.WorldEventFileStore();
        var eventLog = eventStore.Load(worldFile);
        var gameMaster = new OllamaAiGameMaster(
            ollamaHttpClient,
            settings.Model!);
        var result = await new AiGmTurnExecutor(gameMaster)
            .ExecuteAsync(
                eventLog,
                proposal,
                cancellationToken);
        SetNotice(result.Message);
        if (result.WorldChanged)
        {
            eventStore.Save(worldFile, result.EventLog);
        }
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        SetNotice(
            "Ollama non ha potuto completare il turno: " +
            UserFacingErrors.Describe(exception) +
            " L'azione resta al GM umano.");
    }
}

bool QueueConfiguredAiGm(
    string worldFile,
    TessitoreGM.Events.PlayerActionProposal proposal)
{
    if (!aiGmActionsInProgress.TryAdd(proposal.Id, 0))
    {
        return false;
    }

    if (Path.GetFullPath(activeWorldFile).Equals(
        Path.GetFullPath(worldFile),
        StringComparison.OrdinalIgnoreCase))
    {
        aiGmRuntimeNotice =
            "Ollama sta elaborando la risposta in background.";
    }
    _ = Task.Run(async () =>
    {
        var lockAcquired = false;
        try
        {
            await worldLock.WaitAsync(app.Lifetime.ApplicationStopping);
            lockAcquired = true;
            await RunConfiguredAiGmAsync(
                worldFile,
                proposal,
                app.Lifetime.ApplicationStopping);
        }
        catch (OperationCanceledException)
            when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
        {
            // L'arresto conserva l'azione persistita, disponibile al riavvio.
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            if (Path.GetFullPath(activeWorldFile).Equals(
                Path.GetFullPath(worldFile),
                StringComparison.OrdinalIgnoreCase))
            {
                aiGmRuntimeNotice =
                    "Ollama non ha potuto avviare il turno: " +
                    UserFacingErrors.Describe(exception) +
                    " L'azione resta al GM umano.";
            }
        }
        finally
        {
            if (lockAcquired)
            {
                worldLock.Release();
            }
            aiGmActionsInProgress.TryRemove(proposal.Id, out _);
        }
    });
    return true;
}

app.Use(async (context, next) =>
{
    if (accessGate.IsPublicPath(context.Request.Path) ||
        playerAccessGate.IsPublicPath(context.Request.Path) ||
        accessGate.IsAuthorized(context) ||
        playerAccessGate.IsAuthorizedRequest(context))
    {
        await next();
        return;
    }

    if (HttpMethods.IsGet(context.Request.Method))
    {
        if (playerAccessGate.TryGetAuthorizedEntity(
            context,
            out var authorizedPlayer))
        {
            context.Response.Redirect(
                $"/player/{Uri.EscapeDataString(authorizedPlayer.ToString())}");
            return;
        }

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

app.MapGet("/player-login", () => Results.Content(
    WorldDashboard.RenderPlayerLogin(error: false),
    "text/html; charset=utf-8"));
app.MapPost("/player-login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (playerAccessGate.TrySignIn(
        form["accessCode"].ToString(),
        context.Response,
        out var entityId))
    {
        context.Response.Redirect(
            $"/player/{Uri.EscapeDataString(entityId.ToString())}");
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.WriteAsync(
        WorldDashboard.RenderPlayerLogin(error: true));
});

app.MapGet("/", () => Results.Content(
    WorldDashboard.Render(
        activeWorldFile,
        actionToken,
        campaignCatalog.Discover(),
        pendingAdvance,
        focusedScene,
        aiGmSettingsStore.Get(activeWorldFile),
        aiGmRuntimeNotice,
        aiGmActionsInProgress.Keys.ToHashSet()),
    "text/html; charset=utf-8"));
app.MapGet("/chronicle", () => Results.Content(
    WorldDashboard.RenderChronicle(activeWorldFile),
    "text/html; charset=utf-8"));
app.MapGet("/diagnostics", () => Results.Content(
    WorldDashboard.RenderDiagnostics(activeWorldFile, loadedPlugins),
    "text/html; charset=utf-8"));
app.MapGet("/editor", () => Results.Content(
    WorldDashboard.RenderWorldEditor(activeWorldFile, actionToken),
    "text/html; charset=utf-8"));
app.MapGet("/player/{entityId}", (string entityId) => Results.Content(
    WorldDashboard.RenderPlayer(
        activeWorldFile,
        entityId,
        playerInteractionToken,
        aiGmActionsInProgress.Keys.ToHashSet()),
    "text/html; charset=utf-8"));
app.MapGet("/player/{entityId}/version", (string entityId) =>
    Results.Json(new
    {
        version = WorldDashboard.PlayerViewVersion(
            activeWorldFile,
            entityId,
            aiGmActionsInProgress.Keys.ToHashSet())
    }));
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
        focusedScene = null;
        aiGmRuntimeNotice = null;
        playerAccessGate.RevokeAll();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        focusedScene = null;
        aiGmRuntimeNotice = null;
        playerAccessGate.RevokeAll();
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/campaign/restore-backup", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        new TessitoreGM.Events.WorldEventFileStore().RestoreBackup(
            activeWorldFile,
            form["backup"].ToString());
        pendingAdvance = null;
        focusedScene = null;
        aiGmRuntimeNotice = null;
        playerAccessGate.RevokeAll();
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/editor/location", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.AddEditorLocation(
            activeWorldFile,
            form["id"].ToString(),
            form["name"].ToString());
        pendingAdvance = null;
        focusedScene = null;
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/editor");
});
app.MapPost("/editor/resource", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.AddEditorResource(
            activeWorldFile,
            form["id"].ToString(),
            form["name"].ToString());
        pendingAdvance = null;
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/editor");
});
app.MapPost("/editor/npc", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.AddEditorNpc(
            activeWorldFile,
            form["id"].ToString(),
            form["name"].ToString(),
            form["role"].ToString(),
            form["location"].ToString());
        pendingAdvance = null;
        focusedScene = null;
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/editor");
});
app.MapPost("/editor/npc-routine", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.AddEditorNpcRoutine(
            activeWorldFile,
            form["npc"].ToString(),
            form["destination"].ToString(),
            form["time"].ToString());
        pendingAdvance = null;
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/editor");
});
app.MapPost("/ai-gm/mode", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var enabledValue = form["enabled"].ToString();
    if (form["token"] != actionToken ||
        enabledValue is not ("true" or "false"))
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        aiGmSettingsStore.SetEnabled(
            activeWorldFile,
            enabled: enabledValue == "true");
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/#ai-gm-mode");
});
app.MapPost("/ai-gm/ollama", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        var model = form["model"].ToString();
        _ = new OllamaAiGameMaster(
            ollamaHttpClient,
            model);
        var settings = aiGmSettingsStore.SetProvider(
            activeWorldFile,
            "ollama",
            model);
        aiGmRuntimeNotice =
            $"Modello {settings.Model} salvato; Ollama verrà verificato alla prossima azione.";
    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/#ai-gm-mode");
});
app.MapPost("/scene/focus", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        var scene = form["scene"].ToString().Trim();
        focusedScene = string.IsNullOrEmpty(scene) ? null : scene;
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/#scene-focus");
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
            TimeSpan.FromHours(hours),
            loadedPlugins.Rules);
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/player-access", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    await worldLock.WaitAsync();
    try
    {
        var entityId = form["entity"].ToString();
        var playerName = WorldDashboard.PlayerCharacterName(
            activeWorldFile,
            entityId);
        var accessCode = playerAccessGate.IssueCode(entityId);
        return Results.Content(
            WorldDashboard.RenderPlayerAccessCode(
                playerName,
                accessCode),
            "text/html; charset=utf-8");
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
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
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    return Results.Redirect("/");
});
app.MapPost("/player/{entityId}/actions", async (
    HttpContext context,
    string entityId) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != playerInteractionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }
    TessitoreGM.Events.PlayerActionProposal? proposal = null;
    var worldFile = activeWorldFile;
    await worldLock.WaitAsync();
    try
    {
        proposal = WorldDashboard.SubmitPlayerAction(
            worldFile,
            entityId,
            form["description"].ToString());
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
    QueueConfiguredAiGm(worldFile, proposal!);
    return Results.Redirect($"/player/{Uri.EscapeDataString(entityId)}");
});
app.MapPost("/ai-gm/actions/retry", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken ||
        !Guid.TryParse(form["actionId"].ToString(), out var actionId))
    {
        return Results.BadRequest("Richiesta non valida.");
    }

    TessitoreGM.Events.PlayerActionProposal? action = null;
    var worldFile = activeWorldFile;
    await worldLock.WaitAsync();
    try
    {
        var eventLog = new TessitoreGM.Events.WorldEventFileStore()
            .Load(worldFile);
        if ((eventLog.AiGmTurns ?? []).Any(turn =>
            turn.PlayerActionId == actionId))
        {
            throw new InvalidOperationException(
                "Questa azione ha già un turno del Game Master AI.");
        }
        action = (eventLog.PlayerActions ?? [])
            .SingleOrDefault(candidate =>
                candidate.Id == actionId &&
                candidate.Status is
                    TessitoreGM.Events.PlayerActionStatus.Pending or
                    TessitoreGM.Events.PlayerActionStatus.Rolled)
            ?? throw new InvalidOperationException(
                "L'azione non è più disponibile per Ollama.");

    }
    catch (Exception exception) when (
        exception is IOException or InvalidDataException or
        InvalidOperationException or ArgumentException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }

    if (!QueueConfiguredAiGm(worldFile, action!))
    {
        return Results.Conflict(
            "Ollama sta già elaborando questa azione.");
    }

    return Results.Redirect("/#gm-actions");
});
app.MapPost("/player-actions/resolve", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }
    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.ResolvePlayerAction(
            activeWorldFile,
            form["actionId"].ToString(),
            form["decision"].ToString(),
            form["resolution"].ToString());
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
    return Results.Redirect("/");
});
app.MapPost("/ai-gm/consequences/resolve", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }
    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.ResolveAiGmConsequence(
            activeWorldFile,
            form["consequenceId"].ToString(),
            form["decision"].ToString(),
            form["resolution"].ToString());
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException or
        InvalidDataException or IOException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
    return Results.Redirect("/#ai-confirmations");
});
app.MapPost("/player-actions/request-roll", async (
    HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != actionToken ||
        !int.TryParse(form["modifier"], out var modifier))
    {
        return Results.BadRequest("Richiesta non valida.");
    }
    int? difficulty = int.TryParse(form["difficulty"], out var parsedDifficulty)
        ? parsedDifficulty
        : null;
    await worldLock.WaitAsync();
    try
    {
        WorldDashboard.RequestD20Roll(
            activeWorldFile,
            form["actionId"].ToString(),
            modifier,
            difficulty,
            form["difficultyVisible"] == "on",
            form["mode"].ToString());
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
    return Results.Redirect("/");
});
app.MapPost("/player/{entityId}/roll", async (
    HttpContext context,
    string entityId) =>
{
    var form = await context.Request.ReadFormAsync();
    if (form["token"] != playerInteractionToken)
    {
        return Results.BadRequest("Richiesta non valida.");
    }
    TessitoreGM.Events.PlayerActionProposal? rolledAction = null;
    var worldFile = activeWorldFile;
    await worldLock.WaitAsync();
    try
    {
        rolledAction = WorldDashboard.RollD20(
            worldFile,
            entityId,
            form["actionId"].ToString());
    }
    catch (Exception exception) when (
        exception is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(UserFacingErrors.Describe(exception));
    }
    finally
    {
        worldLock.Release();
    }
    QueueConfiguredAiGm(worldFile, rolledAction!);
    return Results.Redirect($"/player/{Uri.EscapeDataString(entityId)}");
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
    Console.WriteLine($"Codice di accesso GM: {accessGate.AccessCode}");
    foreach (var address in LanAccessGate.LocalAddresses())
    {
        Console.WriteLine($"Apri sul telefono: http://{address}:{port}");
    }
}
if (browserEnabled)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                $"http://localhost:{port}")
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Console.WriteLine(
                $"Apri manualmente http://localhost:{port} ({exception.Message})");
        }
    });
}
app.Lifetime.ApplicationStopping.Register(ollamaHttpClient.Dispose);
app.Run(lanEnabled
    ? $"http://0.0.0.0:{port}"
    : $"http://127.0.0.1:{port}");
