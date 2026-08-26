using System.Net;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
using TessitoreGM.AiGm;
using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;
using TessitoreGM.Npcs;
using TessitoreGM.World;

namespace TessitoreGM.Gm;

internal static class WorldDashboard
{
    public static string RenderLogin(bool error) => Page(
        "Accesso al Tavolo",
        "<main class=\"login-shell\"><section class=\"login-card\"><p class=\"eyebrow\">TessitoreGM</p><h1>Entra al tavolo.</h1><p>Inserisci il codice temporaneo mostrato sul computer del Game Master.</p>" +
        (error
            ? "<p class=\"login-error\">Codice non valido o accesso temporaneamente bloccato.</p>"
            : string.Empty) +
        "<form method=\"post\" action=\"/login\"><label for=\"access-code\">Codice di accesso GM</label><input id=\"access-code\" name=\"accessCode\" inputmode=\"numeric\" autocomplete=\"one-time-code\" pattern=\"[0-9]{8}\" maxlength=\"8\" required autofocus><button type=\"submit\">Accedi come GM</button></form><a class=\"login-switch\" href=\"/player-login\">Sei un giocatore? Accedi al tuo personaggio</a></section></main>");

    public static string RenderPlayerLogin(bool error) => Page(
        "Accesso del giocatore",
        "<main class=\"login-shell\"><section class=\"login-card\"><p class=\"eyebrow\">TessitoreGM</p><h1>Entra in scena.</h1><p>Inserisci il codice monouso che il Game Master ha generato per il tuo personaggio.</p>" +
        (error
            ? "<p class=\"login-error\">Codice non valido, già utilizzato o accesso temporaneamente bloccato.</p>"
            : string.Empty) +
        "<form method=\"post\" action=\"/player-login\"><label for=\"player-access-code\">Codice del personaggio</label><input id=\"player-access-code\" name=\"accessCode\" inputmode=\"numeric\" autocomplete=\"one-time-code\" pattern=\"[0-9]{8}\" maxlength=\"8\" required autofocus><button type=\"submit\">Accedi al personaggio</button></form><a class=\"login-switch\" href=\"/login\">Torna all'accesso del GM</a></section></main>");

    public static string RenderPlayerAccessCode(
        string playerName,
        string accessCode) => Page(
        "Codice del giocatore",
        "<main class=\"empty-state access-code-page\"><p class=\"eyebrow\">Accesso personale</p>" +
        $"<h1>{Encode(playerName)}</h1><p>Comunica questo codice al giocatore. È monouso, resta valido fino all'accesso e sostituisce ogni precedente sessione del personaggio.</p>" +
        $"<strong class=\"access-code\">{Encode(accessCode)}</strong>" +
        "<p>Il giocatore deve aprire l'indirizzo del Tavolo, scegliere <strong>Accedi al tuo personaggio</strong> e inserire il codice.</p><a class=\"button\" href=\"/\">Torna al Tavolo del GM</a></main>");

    public static string ResolveWorldFile(
        string[] args,
        string launchDirectory)
    {
        if (string.IsNullOrWhiteSpace(launchDirectory))
        {
            throw new ArgumentException(
                "The launch directory cannot be empty.",
                nameof(launchDirectory));
        }

        var suppliedPath = args.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal))
            ?? "village.json";

        if (Path.IsPathFullyQualified(suppliedPath))
        {
            return Path.GetFullPath(suppliedPath);
        }

        for (var directory = new DirectoryInfo(launchDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.GetFullPath(suppliedPath, directory.FullName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(suppliedPath, launchDirectory);
    }

    public static string Render(
        string worldFile,
        string actionToken,
        IReadOnlyList<CampaignEntry> campaigns,
        PendingWorldAdvance? pendingAdvance,
        string? focusedScene,
        AiGmCampaignModeSettings aiGmSettings,
        string? aiGmRuntimeNotice = null,
        IReadOnlySet<Guid>? aiGmActionsInProgress = null)
    {
        try
        {
            return File.Exists(worldFile)
                ? RenderWorld(
                    worldFile,
                    actionToken,
                    campaigns,
                    pendingAdvance,
                    focusedScene,
                    aiGmSettings,
                    aiGmRuntimeNotice,
                    aiGmActionsInProgress ?? new HashSet<Guid>())
                : RenderMissingWorld(
                    worldFile,
                    actionToken,
                    campaigns);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            return RenderUnreadableWorld(
                worldFile,
                actionToken,
                exception);
        }
    }

    public static string RenderChronicle(string worldFile)
    {
        try
        {
            var eventLog = new WorldEventJsonSerializer().Deserialize(
                File.ReadAllText(worldFile));
            var simulation = eventLog.Simulation;
            var entityNames = EntityNames(simulation, eventLog.Events);
            var locationNames = simulation?.Locations?.ToDictionary(
                location => location.LocationId,
                location => location.Name) ?? new Dictionary<LocationId, string>();
            var resourceNames = simulation?.Resources?.ToDictionary(
                resource => resource.ResourceId,
                resource => resource.Name) ?? new Dictionary<ResourceId, string>();
            var needNames = simulation?.Needs?.ToDictionary(
                need => need.NeedId,
                need => need.Name) ?? new Dictionary<NeedId, string>();
            var narration = new DeterministicNarrator().Narrate(
                eventLog.Events,
                new NarrationContext(
                    entityNames,
                    locationNames,
                    resourceNames,
                    needNames));
            var firstTime = narration.Count > 0
                ? narration[0].OccurredAt
                : eventLog.InitialWorld.CurrentTime;
            var lastTime = narration.Count > 0
                ? narration[^1].OccurredAt
                : eventLog.InitialWorld.CurrentTime;
            var playerActions = eventLog.Events
                .OfType<PlayerActionRecorded>()
                .Count();
            var content = new StringBuilder();

            content.Append("<header class=\"hero chronicle-hero\"><div><p class=\"eyebrow\">Cronaca della campagna</p><h1>Ciò che è accaduto</h1><p class=\"lede\">Il racconto completo e deterministico ricostruito dal registro del mondo.</p></div><div class=\"hero-actions\"><a class=\"button secondary\" href=\"/\">Torna al tavolo</a><button type=\"button\" onclick=\"window.print()\">Stampa</button></div></header>");
            content.Append("<main><section class=\"world-strip chronicle-summary\">");
            content.Append(Metric(
                "Intervallo",
                $"{firstTime:dd MMM yyyy · HH:mm} – {lastTime:dd MMM yyyy · HH:mm}"));
            content.Append(Metric("Avvenimenti", narration.Count.ToString()));
            content.Append(Metric("Azioni dei giocatori", playerActions.ToString()));
            content.Append("</section><section class=\"chronicle chronicle-full\"><div class=\"section-heading\"><p class=\"eyebrow\">Registro completo</p><h2>Tutti gli avvenimenti</h2></div><ol>");
            foreach (var line in narration)
            {
                content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM/yyyy · HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
            }
            if (narration.Count == 0)
            {
                content.Append("<li><p>La campagna non contiene ancora avvenimenti.</p></li>");
            }
            content.Append("</ol></section></main><footer><span>Campagna</span>");
            content.Append($"<code>{Encode(Path.GetFileName(worldFile))}</code></footer>");
            return Page("Cronaca", content.ToString());
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            return Page(
                "Cronaca non disponibile",
                $"<main class=\"empty-state\"><p class=\"eyebrow\">Cronaca</p><h1>Non riesco a generarla.</h1><p>{Encode(exception.Message)}</p><a class=\"button secondary\" href=\"/\">Torna al tavolo</a></main>");
        }
    }

    public static string RenderDiagnostics(string worldFile)
    {
        var content = new StringBuilder();
        content.Append("<header class=\"hero chronicle-hero\"><div><p class=\"eyebrow\">Diagnostica</p><h1>Stato di TessitoreGM</h1><p class=\"lede\">Controlli leggibili del salvataggio attivo e delle copie di sicurezza.</p></div><div class=\"hero-actions\"><a class=\"button secondary\" href=\"/\">Torna al tavolo</a></div></header><main>");
        try
        {
            var store = new WorldEventFileStore();
            var eventLog = store.Load(worldFile);
            var world = Replay(eventLog);
            var backups = store.ListBackups(worldFile);
            content.Append("<section class=\"world-strip diagnostic-summary\">");
            content.Append(Metric("Salvataggio", "Leggibile"));
            content.Append(Metric("Ora del mondo", world.CurrentTime.ToString("dd MMM yyyy · HH:mm")));
            content.Append(Metric("Eventi", eventLog.Events.Count.ToString()));
            content.Append(Metric("Backup", backups.Count.ToString()));
            content.Append("</section><section class=\"player-panel diagnostic-detail\"><h2>Percorsi</h2>");
            content.Append($"<p><strong>Campagna attiva</strong><code>{Encode(Path.GetFullPath(worldFile))}</code></p>");
            content.Append($"<p><strong>Cartella backup</strong><code>{Encode(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(worldFile))!, "Backups"))}</code></p>");
            content.Append($"<p><strong>Versione applicazione</strong><code>{Encode(typeof(WorldDashboard).Assembly.GetName().Version?.ToString() ?? "non disponibile")}</code></p></section>");
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            content.Append("<section class=\"recovery-panel\"><p class=\"eyebrow\">Problema rilevato</p><h2>Il salvataggio non è leggibile</h2>");
            content.Append($"<p>{Encode(exception.Message)}</p><code>{Encode(Path.GetFullPath(worldFile))}</code><p>Torna al Tavolo per scegliere un backup disponibile.</p></section>");
        }

        content.Append("</main>");
        return Page("Diagnostica", content.ToString());
    }

    public static string RenderPlayer(
        string worldFile,
        string entityValue,
        string playerInteractionToken,
        IReadOnlySet<Guid>? aiGmActionsInProgress = null)
    {
        try
        {
            var serializedWorld = File.ReadAllText(worldFile);
            var eventLog = new WorldEventJsonSerializer().Deserialize(
                serializedWorld);
            var world = Replay(eventLog);
            var simulation = eventLog.Simulation;
            var playerCharacters = PlayerCharacters(eventLog.Events);
            var character = playerCharacters.FirstOrDefault(candidate =>
                candidate.EntityId.ToString() == entityValue)
                ?? throw new ArgumentException(
                    "Il personaggio giocante richiesto non esiste.");
            var entityNames = EntityNames(simulation, eventLog.Events);
            var locationNames = simulation?.Locations?.ToDictionary(
                location => location.LocationId,
                location => location.Name) ??
                new Dictionary<LocationId, string>();
            var resourceNames = simulation?.Resources?.ToDictionary(
                resource => resource.ResourceId,
                resource => resource.Name) ??
                new Dictionary<ResourceId, string>();
            var location = world.GetLocation(character.EntityId);
            var locationName = location is LocationId locationId
                ? locationNames.GetValueOrDefault(
                    locationId,
                    locationId.ToString())
                : "Fuori scena";
            var knownFacts = KnownFacts(character.EntityId, eventLog.Events);
            var visibleEntities = (simulation?.Npcs ??
                    Array.Empty<NpcSimulationDefinition>())
                .Select(npc => npc.EntityId)
                .Concat(playerCharacters.Select(player => player.EntityId))
                .Where(entityId =>
                    entityId != character.EntityId &&
                    location is not null &&
                    world.GetLocation(entityId) == location)
                .Select(entityId => entityNames.GetValueOrDefault(
                    entityId,
                    entityId.ToString()))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var resources = (simulation?.Resources ??
                    Array.Empty<ResourcePresentationDefinition>())
                .Select(resource => new
                {
                    Name = resourceNames.GetValueOrDefault(
                        resource.ResourceId,
                        resource.ResourceId.ToString()),
                    Quantity = world.GetResourceQuantity(
                        character.EntityId,
                        resource.ResourceId)
                })
                .Where(resource => resource.Quantity > 0)
                .ToArray();
            var ownActions = (eventLog.PlayerActions ??
                    Array.Empty<PlayerActionProposal>())
                .Where(action =>
                    action.PlayerCharacterId == character.EntityId)
                .OrderByDescending(action => action.SubmittedAt)
                .Take(10)
                .ToArray();
            var processingActions = aiGmActionsInProgress ??
                new HashSet<Guid>();
            var hasAiProcessing = ownActions.Any(action =>
                processingActions.Contains(action.Id));
            var aiTurnsByAction = (eventLog.AiGmTurns ??
                    Array.Empty<AiGmTurnRecord>())
                .GroupBy(turn => turn.PlayerActionId)
                .ToDictionary(group => group.Key, group => group.Last());
            var hasOutstandingAction = ownActions.Any(action =>
                action.Status is PlayerActionStatus.Pending or
                    PlayerActionStatus.RollRequested or
                    PlayerActionStatus.Rolled);
            var needNames = simulation?.Needs?.ToDictionary(
                need => need.NeedId,
                need => need.Name) ?? new Dictionary<NeedId, string>();
            var visibleEvents = ObservableEvents(
                    eventLog.Events,
                    character,
                    location)
                .TakeLast(8)
                .ToArray();
            var visibleNarration = new DeterministicNarrator().Narrate(
                visibleEvents,
                new NarrationContext(
                    entityNames,
                    locationNames,
                    resourceNames,
                    needNames));
            var viewVersion = ContentVersion(serializedWorld) +
                (hasAiProcessing ? ":ai" : ":idle");
            var content = new StringBuilder();

            content.Append("<header class=\"player-hero\"><div><p class=\"eyebrow\">Tavolo del giocatore</p>");
            content.Append($"<h1>{Encode(character.Name)}</h1><p class=\"lede\">Ciò che il tuo personaggio può osservare e ricordare, adesso.</p></div><div class=\"player-live-controls\"><span class=\"live-status\"><i></i>Sincronizzazione attiva</span><button type=\"button\" class=\"secondary\" onclick=\"location.reload()\">Aggiorna ora</button></div></header>");
            content.Append("<main class=\"player-view\"><section class=\"world-strip player-summary\">");
            content.Append(Metric("Luogo", locationName));
            content.Append(Metric(
                "Monete",
                world.GetBalance(character.EntityId).ToString()));
            content.Append(Metric("Clima", WeatherLabel(world.Weather)));
            content.Append(Metric("Ora del mondo", world.CurrentTime.ToString(
                "dd MMM yyyy · HH:mm")));
            content.Append("</section><section class=\"player-action-box\"><div class=\"section-heading\"><p class=\"eyebrow\">La tua azione</p><h2>Cosa vuoi fare?</h2></div>");
            if (hasOutstandingAction)
            {
                content.Append(hasAiProcessing
                    ? "<p class=\"player-action-wait ai-processing\"><i></i>Ollama sta elaborando la risposta. Puoi lasciare aperta questa pagina: si aggiornerà automaticamente.</p>"
                    : "<p class=\"player-action-wait\">La tua azione precedente è ancora in corso. Potrai dichiararne un'altra dopo la risposta del Game Master.</p>");
            }
            else
            {
                content.Append("<form method=\"post\" action=\"/player/");
                content.Append(Uri.EscapeDataString(character.EntityId.ToString()));
                content.Append("/actions\">");
                content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(playerInteractionToken)}\"><label for=\"proposed-action\">Descrivi l'intenzione del personaggio</label><textarea id=\"proposed-action\" name=\"description\" maxlength=\"500\" rows=\"4\" placeholder=\"Cerco tracce vicino al granaio.\" required></textarea><button type=\"submit\">Invia al GM</button></form>");
            }
            content.Append("</section>");
            content.Append("<section><div class=\"section-heading\"><p class=\"eyebrow\">Richieste</p><h2>Le tue azioni</h2></div><div class=\"player-action-list\">");
            if (ownActions.Length == 0)
            {
                content.Append("<p class=\"empty-copy\">Non hai ancora proposto azioni.</p>");
            }
            foreach (var action in ownActions)
            {
                content.Append("<article class=\"player-action-item\">");
                content.Append($"<div><span class=\"action-status\">{Encode(PlayerActionStatusLabel(action.Status))}</span><p>{Encode(action.Description)}</p></div>");
                if (processingActions.Contains(action.Id))
                {
                    content.Append("<div class=\"action-resolution ai-processing\"><i></i><strong>Game Master AI in elaborazione</strong><p>La pagina mostrerà automaticamente la risposta appena sarà pronta.</p></div>");
                }
                if (action.Roll is not null)
                {
                    content.Append(RenderRollForPlayer(
                        action,
                        character.EntityId,
                        playerInteractionToken));
                }
                if (!string.IsNullOrWhiteSpace(action.Resolution))
                {
                    content.Append($"<div class=\"action-resolution\"><strong>Esito del GM</strong><p>{Encode(action.Resolution)}</p></div>");
                }
                if (aiTurnsByAction.GetValueOrDefault(action.Id) is { } aiTurn)
                {
                    if (aiTurn.Status == AiGmTurnStatus.PendingConfirmation)
                    {
                        content.Append("<div class=\"action-resolution ai-player-narration\"><strong>Game Master AI</strong><p>È necessaria la conferma del GM per una conseguenza importante.</p></div>");
                    }
                    else if (aiTurn.Consequences.All(consequence =>
                        consequence.Status == AiGmConsequenceStatus.Applied))
                    {
                        content.Append($"<div class=\"action-resolution ai-player-narration\"><strong>Game Master AI</strong><p>{Encode(aiTurn.Narration)}</p></div>");
                    }
                    else
                    {
                        content.Append("<div class=\"action-resolution ai-player-narration\"><strong>Game Master AI</strong><p>Il turno è concluso; il GM ha modificato una conseguenza proposta.</p></div>");
                    }
                }
                content.Append("</article>");
            }
            content.Append("</div></section><section><div class=\"section-heading\"><p class=\"eyebrow\">Scena attuale</p><h2>Chi è qui</h2></div><div class=\"player-panel\">");
            content.Append(visibleEntities.Length == 0
                ? "<p>Nessun altro personaggio è visibile in questo luogo.</p>"
                : $"<ul>{string.Join(string.Empty, visibleEntities.Select(name => $"<li>{Encode(name)}</li>"))}</ul>");
            content.Append("</div></section><section><div class=\"section-heading\"><p class=\"eyebrow\">Equipaggiamento</p><h2>Risorse possedute</h2></div><div class=\"player-panel\">");
            content.Append(resources.Length == 0
                ? "<p>Non possiedi risorse registrate.</p>"
                : $"<div class=\"player-resource-grid\">{string.Join(string.Empty, resources.Select(resource => Stat(resource.Name, resource.Quantity.ToString())))}</div>");
            content.Append("</div></section><section><div class=\"section-heading\"><p class=\"eyebrow\">Memoria</p><h2>Conoscenze</h2></div><div class=\"player-panel\">");
            content.Append(knownFacts.Count == 0
                ? "<p>Non conosci ancora informazioni registrate.</p>"
                : $"<ul>{string.Join(string.Empty, knownFacts.OrderBy(fact => fact.ToString(), StringComparer.Ordinal).Select(fact => $"<li>{Encode(fact.ToString())}</li>"))}</ul>");
            content.Append("</div></section><section class=\"chronicle\"><div class=\"section-heading\"><p class=\"eyebrow\">Ciò che osservi</p><h2>Avvenimenti recenti</h2></div><ol>");
            foreach (var line in visibleNarration.Reverse())
            {
                content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM · HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
            }
            if (visibleNarration.Count == 0)
            {
                content.Append("<li><p>Non hai osservato nuovi avvenimenti.</p></li>");
            }
            content.Append("</ol></section></main><footer><span>Campagna</span>");
            content.Append($"<code>{Encode(Path.GetFileName(worldFile))}</code></footer>");
            content.Append(PlayerLiveUpdate(
                character.EntityId,
                viewVersion));
            return Page(character.Name, content.ToString());
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            return Page(
                "Personaggio non disponibile",
                $"<main class=\"empty-state\"><p class=\"eyebrow\">Tavolo del giocatore</p><h1>Non riesco ad aprire questo personaggio.</h1><p>{Encode(exception.Message)}</p></main>");
        }
    }

    public static string PlayerViewVersion(
        string worldFile,
        string entityValue,
        IReadOnlySet<Guid>? aiGmActionsInProgress = null)
    {
        var serializedWorld = File.ReadAllText(worldFile);
        var eventLog = new WorldEventJsonSerializer().Deserialize(
            serializedWorld);
        var character = PlayerCharacters(eventLog.Events).FirstOrDefault(character =>
            character.EntityId.ToString() == entityValue)
            ?? throw new ArgumentException(
                "Il personaggio giocante richiesto non esiste.");
        var processingActions = aiGmActionsInProgress ?? new HashSet<Guid>();
        var hasAiProcessing = (eventLog.PlayerActions ?? []).Any(action =>
            action.PlayerCharacterId == character.EntityId &&
            processingActions.Contains(action.Id));
        return ContentVersion(serializedWorld) +
            (hasAiProcessing ? ":ai" : ":idle");
    }

    public static PendingWorldAdvance PreviewAdvance(
        string worldFile,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var world = Replay(eventLog);
        var result = new ConfiguredWorldSimulator().Advance(
            eventLog,
            world,
            world.CurrentTime.Add(duration));
        return new PendingWorldAdvance(
            Path.GetFullPath(worldFile),
            eventLog.Events.Count,
            world.CurrentTime,
            result.World.CurrentTime,
            result.ProducedEvents);
    }

    public static void ApproveAdvance(
        string worldFile,
        PendingWorldAdvance proposal)
    {
        if (!Path.GetFullPath(worldFile).Equals(
            proposal.WorldFile,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La proposta appartiene a un'altra campagna.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var world = Replay(eventLog);
        if (eventLog.Events.Count != proposal.BaseEventCount ||
            world.CurrentTime != proposal.BaseTime)
        {
            throw new InvalidOperationException(
                "Il mondo è cambiato: genera una nuova anteprima.");
        }

        _ = new WorldEventProcessor().Replay(world, proposal.Events);
        var updatedLog = eventLog with
        {
            Events = eventLog.Events.Concat(proposal.Events).ToArray()
        };
        Save(worldFile, serializer, updatedLog);
    }

    public static void MoveEntity(
        string worldFile,
        string entityValue,
        string locationValue)
    {
        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var simulation = eventLog.Simulation
            ?? throw new ArgumentException(
                "Il salvataggio non contiene una simulazione configurata.");
        var playerCharacters = PlayerCharacters(eventLog.Events);
        var entityId = simulation.Npcs
            .Select(candidate => candidate.EntityId)
            .Concat(playerCharacters.Select(character => character.EntityId))
            .FirstOrDefault(candidate => candidate.ToString() == entityValue);
        if (entityId == default)
        {
            throw new ArgumentException("Personaggio non valido.");
        }
        var location = (simulation.Locations ??
            Array.Empty<LocationPresentationDefinition>())
            .FirstOrDefault(candidate =>
                candidate.LocationId.ToString() == locationValue)
            ?? throw new ArgumentException("Luogo non valido.");
        var world = Replay(eventLog);

        if (world.GetLocation(entityId) == location.LocationId)
        {
            throw new ArgumentException(
                "Il personaggio si trova già nel luogo scelto.");
        }

        var movement = new EntityEnteredLocation(
            entityId,
            location.LocationId,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            movement);

        Save(worldFile, serializer, updatedLog);
    }

    public static void RevealFact(
        string worldFile,
        string entityValue,
        string factValue,
        string newFactValue)
    {
        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var simulation = eventLog.Simulation
            ?? throw new ArgumentException(
                "Il salvataggio non contiene una simulazione configurata.");
        var npc = simulation.Npcs.FirstOrDefault(candidate =>
            candidate.EntityId.ToString() == entityValue);
        var playerCharacter = PlayerCharacters(eventLog.Events)
            .FirstOrDefault(character =>
                character.EntityId.ToString() == entityValue);
        if (npc is null && playerCharacter is null)
        {
            throw new ArgumentException("Personaggio non valido.");
        }
        var entityId = npc?.EntityId ?? playerCharacter!.EntityId;
        var suppliedNewFact = newFactValue.Trim();
        if (suppliedNewFact.Length > 100)
        {
            throw new ArgumentException(
                "L'identificatore dell'informazione è troppo lungo.");
        }
        var factId = suppliedNewFact.Length > 0
            ? new FactId(suppliedNewFact)
            : AvailableFacts(simulation, eventLog.Events)
                .FirstOrDefault(candidate => candidate.ToString() == factValue);

        if (factId == default)
        {
            throw new ArgumentException("Informazione non valida.");
        }

        var knownFacts = npc is not null
            ? KnownFacts(npc, eventLog.Events)
            : KnownFacts(entityId, eventLog.Events);
        if (knownFacts.Contains(factId))
        {
            throw new ArgumentException(
                "Il personaggio conosce già questa informazione.");
        }

        var world = Replay(eventLog);
        var revealed = new FactRevealed(
            entityId,
            factId,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            revealed);

        Save(worldFile, serializer, updatedLog);
    }

    public static void TransferCoins(
        string worldFile,
        string payerValue,
        string payeeValue,
        int amount,
        string reasonValue)
    {
        if (amount <= 0)
        {
            throw new ArgumentException(
                "La quantità di monete deve essere maggiore di zero.");
        }
        var reason = reasonValue.Trim();
        if (reason.Length is < 1 or > 200)
        {
            throw new ArgumentException(
                "La motivazione deve contenere da 1 a 200 caratteri.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var validEntities = (eventLog.Simulation?.Npcs ??
                Array.Empty<NpcSimulationDefinition>())
            .Select(npc => npc.EntityId)
            .Concat(PlayerCharacters(eventLog.Events).Select(
                character => character.EntityId))
            .ToArray();
        var payerId = validEntities.FirstOrDefault(
            entityId => entityId.ToString() == payerValue);
        var payeeId = validEntities.FirstOrDefault(
            entityId => entityId.ToString() == payeeValue);
        if (payerId == default || payeeId == default)
        {
            throw new ArgumentException("Personaggio non valido.");
        }

        var world = Replay(eventLog);
        var transferred = new CoinsTransferred(
            payerId,
            payeeId,
            amount,
            reason,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            transferred);
        Save(worldFile, serializer, updatedLog);
    }

    public static void ApplyResourceConsequence(
        string worldFile,
        string operation,
        string entityValue,
        string destinationValue,
        string resourceValue,
        int quantity,
        string reasonValue)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException(
                "La quantità deve essere maggiore di zero.");
        }
        var reason = reasonValue.Trim();
        if (reason.Length is < 1 or > 200)
        {
            throw new ArgumentException(
                "La motivazione deve contenere da 1 a 200 caratteri.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var simulation = eventLog.Simulation
            ?? throw new ArgumentException(
                "Il salvataggio non contiene risorse configurate.");
        var entities = simulation.Npcs.Select(npc => npc.EntityId)
            .Concat(PlayerCharacters(eventLog.Events).Select(
                character => character.EntityId))
            .ToArray();
        var entityId = entities.FirstOrDefault(
            candidate => candidate.ToString() == entityValue);
        var destinationId = entities.FirstOrDefault(
            candidate => candidate.ToString() == destinationValue);
        var resourceId = (simulation.Resources ??
                Array.Empty<ResourcePresentationDefinition>())
            .Select(resource => resource.ResourceId)
            .FirstOrDefault(candidate => candidate.ToString() == resourceValue);
        if (entityId == default || resourceId == default ||
            (operation == "transfer" && destinationId == default))
        {
            throw new ArgumentException("Conseguenza non valida.");
        }

        var world = Replay(eventLog);
        IWorldEvent consequence = operation switch
        {
            "acquire" => new ResourceAcquired(
                entityId,
                resourceId,
                quantity,
                reason,
                world.CurrentTime),
            "lose" => new ResourceLost(
                entityId,
                resourceId,
                quantity,
                reason,
                world.CurrentTime),
            "transfer" => new ResourceTransferred(
                entityId,
                destinationId,
                resourceId,
                quantity,
                reason,
                world.CurrentTime),
            _ => throw new ArgumentException("Operazione non valida.")
        };
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            consequence);
        Save(worldFile, serializer, updatedLog);
    }

    public static void RecordPlayerAction(
        string worldFile,
        string actorValue,
        string descriptionValue)
    {
        var description = descriptionValue.Trim();
        if (description.Length is < 1 or > 500)
        {
            throw new ArgumentException(
                "L'azione deve contenere da 1 a 500 caratteri.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var character = PlayerCharacters(eventLog.Events).FirstOrDefault(
            candidate => candidate.EntityId.ToString() == actorValue);
        if (character is null)
        {
            throw new ArgumentException("Personaggio giocante non valido.");
        }
        var world = Replay(eventLog);
        var playerAction = new PlayerActionRecorded(
            character.Name,
            description,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            playerAction);

        Save(worldFile, serializer, updatedLog);
    }

    public static void RegisterPlayerCharacter(
        string worldFile,
        string nameValue)
    {
        var name = nameValue.Trim();
        if (name.Length is < 1 or > 80)
        {
            throw new ArgumentException(
                "Il nome deve contenere da 1 a 80 caratteri.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var playerCharacters = PlayerCharacters(eventLog.Events);
        if (playerCharacters.Any(character =>
            character.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Esiste già un personaggio giocante con questo nome.");
        }

        var usedIds = (eventLog.Simulation?.Npcs ??
                Array.Empty<NpcSimulationDefinition>())
            .Select(npc => npc.EntityId)
            .Concat(playerCharacters.Select(character => character.EntityId))
            .ToHashSet();
        var baseValue = "pc:" + Slug(name);
        var candidateValue = baseValue;
        var suffix = 2;
        while (usedIds.Contains(new EntityId(candidateValue)))
        {
            candidateValue = $"{baseValue}-{suffix++}";
        }

        var world = Replay(eventLog);
        var registered = new PlayerCharacterRegistered(
            new EntityId(candidateValue),
            name,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            registered);
        Save(worldFile, serializer, updatedLog);
    }

    public static string PlayerCharacterName(
        string worldFile,
        string entityValue)
    {
        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var character = PlayerCharacters(eventLog.Events).FirstOrDefault(
            candidate => candidate.EntityId.ToString() == entityValue);
        return character?.Name ?? throw new ArgumentException(
            "Personaggio giocante non valido.");
    }

    public static PlayerActionProposal SubmitPlayerAction(
        string worldFile,
        string entityValue,
        string descriptionValue)
    {
        var description = descriptionValue.Trim();
        if (description.Length is < 1 or > 500)
        {
            throw new ArgumentException(
                "L'azione deve contenere da 1 a 500 caratteri.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var character = PlayerCharacters(eventLog.Events).FirstOrDefault(
            candidate => candidate.EntityId.ToString() == entityValue)
            ?? throw new ArgumentException(
                "Personaggio giocante non valido.");
        if ((eventLog.PlayerActions ?? []).Any(action =>
            action.PlayerCharacterId == character.EntityId &&
            action.Status is PlayerActionStatus.Pending or
                PlayerActionStatus.RollRequested or
                PlayerActionStatus.Rolled))
        {
            throw new InvalidOperationException(
                "Questo personaggio ha già un'azione in attesa di risoluzione.");
        }
        var world = Replay(eventLog);
        var proposal = new PlayerActionProposal(
            Guid.NewGuid(),
            character.EntityId,
            description,
            world.CurrentTime);
        var updatedLog = eventLog with
        {
            PlayerActions = (eventLog.PlayerActions ??
                Array.Empty<PlayerActionProposal>())
                .Append(proposal)
                .ToArray()
        };
        Save(worldFile, serializer, updatedLog);
        return proposal;
    }

    public static void ResolvePlayerAction(
        string worldFile,
        string actionValue,
        string decision,
        string resolutionValue)
    {
        if (!Guid.TryParse(actionValue, out var actionId))
        {
            throw new ArgumentException("Azione non valida.");
        }
        var resolution = resolutionValue.Trim();
        if (resolution.Length is < 1 or > 500)
        {
            throw new ArgumentException(
                "L'esito deve contenere da 1 a 500 caratteri.");
        }
        if (decision is not ("approve" or "reject"))
        {
            throw new ArgumentException("Decisione non valida.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var actions = (eventLog.PlayerActions ??
            Array.Empty<PlayerActionProposal>()).ToArray();
        var index = Array.FindIndex(actions, action => action.Id == actionId);
        if (index < 0 || actions[index].Status is not (
            PlayerActionStatus.Pending or
            PlayerActionStatus.RollRequested or
            PlayerActionStatus.Rolled))
        {
            throw new InvalidOperationException(
                "L'azione non è più in attesa di una decisione.");
        }
        var world = Replay(eventLog);
        var status = decision == "approve"
            ? PlayerActionStatus.Approved
            : PlayerActionStatus.Rejected;
        actions[index] = actions[index] with
        {
            Status = status,
            Resolution = resolution,
            ResolvedAt = world.CurrentTime
        };
        var updatedEvents = eventLog.Events;
        if (status == PlayerActionStatus.Approved)
        {
            var character = PlayerCharacters(eventLog.Events).First(
                candidate =>
                    candidate.EntityId == actions[index].PlayerCharacterId);
            updatedEvents = new WorldEventLogEditor().Append(
                eventLog,
                world,
                new PlayerActionRecorded(
                    character.Name,
                    actions[index].Description,
                    world.CurrentTime)).Events;
        }
        Save(worldFile, serializer, eventLog with
        {
            Events = updatedEvents,
            PlayerActions = actions
        });
    }

    public static void ResolveAiGmConsequence(
        string worldFile,
        string consequenceValue,
        string decision,
        string resolution)
    {
        if (!Guid.TryParse(consequenceValue, out var consequenceId) ||
            decision is not ("approve" or "reject"))
        {
            throw new ArgumentException("Decisione AI non valida.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var updatedLog = new AiGmTurnCoordinator().ResolveConsequence(
            eventLog,
            consequenceId,
            approve: decision == "approve",
            resolution: resolution);
        Save(worldFile, serializer, updatedLog);
    }

    public static void RequestD20Roll(
        string worldFile,
        string actionValue,
        int modifier,
        int? difficulty,
        bool difficultyVisible,
        string modeValue)
    {
        if (!Guid.TryParse(actionValue, out var actionId) ||
            modifier is < -20 or > 20 ||
            difficulty is < 1 or > 40 ||
            !Enum.TryParse<D20RollMode>(
                modeValue,
                ignoreCase: true,
                out var mode))
        {
            throw new ArgumentException("Richiesta di tiro non valida.");
        }

        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var actions = (eventLog.PlayerActions ??
            Array.Empty<PlayerActionProposal>()).ToArray();
        var index = Array.FindIndex(actions, action => action.Id == actionId);
        if (index < 0 || actions[index].Status != PlayerActionStatus.Pending)
        {
            throw new InvalidOperationException(
                "L'azione non può ricevere un nuovo tiro.");
        }
        actions[index] = actions[index] with
        {
            Status = PlayerActionStatus.RollRequested,
            Roll = new D20Roll(
                modifier,
                difficulty,
                difficultyVisible,
                mode,
                Replay(eventLog).CurrentTime)
        };
        Save(worldFile, serializer, eventLog with { PlayerActions = actions });
    }

    public static PlayerActionProposal RollD20(
        string worldFile,
        string entityValue,
        string actionValue)
    {
        if (!Guid.TryParse(actionValue, out var actionId))
        {
            throw new ArgumentException("Tiro non valido.");
        }
        var serializer = new WorldEventJsonSerializer();
        var eventLog = serializer.Deserialize(File.ReadAllText(worldFile));
        var actions = (eventLog.PlayerActions ??
            Array.Empty<PlayerActionProposal>()).ToArray();
        var index = Array.FindIndex(actions, action =>
            action.Id == actionId &&
            action.PlayerCharacterId.ToString() == entityValue);
        if (index < 0 ||
            actions[index].Status != PlayerActionStatus.RollRequested ||
            actions[index].Roll is null)
        {
            throw new InvalidOperationException(
                "Questo tiro non è disponibile.");
        }

        var roll = actions[index].Roll!;
        var dice = roll.Mode == D20RollMode.Normal
            ? new[] { RandomNumberGenerator.GetInt32(1, 21) }
            : new[]
            {
                RandomNumberGenerator.GetInt32(1, 21),
                RandomNumberGenerator.GetInt32(1, 21)
            };
        var keptDie = roll.Mode switch
        {
            D20RollMode.Advantage => dice.Max(),
            D20RollMode.Disadvantage => dice.Min(),
            _ => dice[0]
        };
        actions[index] = actions[index] with
        {
            Status = PlayerActionStatus.Rolled,
            Roll = roll with
            {
                Dice = dice,
                KeptDie = keptDie,
                Total = keptDie + roll.Modifier,
                RolledAt = Replay(eventLog).CurrentTime
            }
        };
        Save(worldFile, serializer, eventLog with { PlayerActions = actions });
        return actions[index];
    }

    private static void Save(
        string worldFile,
        WorldEventJsonSerializer serializer,
        WorldEventLog eventLog)
    {
        new WorldEventFileStore(serializer).Save(worldFile, eventLog);
    }

    private static string RenderWorld(
        string worldFile,
        string actionToken,
        IReadOnlyList<CampaignEntry> campaigns,
        PendingWorldAdvance? pendingAdvance,
        string? focusedScene,
        AiGmCampaignModeSettings aiGmSettings,
        string? aiGmRuntimeNotice,
        IReadOnlySet<Guid> aiGmActionsInProgress)
    {
        var eventLog = new WorldEventJsonSerializer().Deserialize(
            File.ReadAllText(worldFile));
        var world = Replay(eventLog);
        var simulation = eventLog.Simulation;
        var playerCharacters = PlayerCharacters(eventLog.Events);
        var entityNames = EntityNames(simulation, eventLog.Events);
        var locationNames = simulation?.Locations?.ToDictionary(
            location => location.LocationId,
            location => location.Name) ?? new Dictionary<LocationId, string>();
        var resourceNames = simulation?.Resources?.ToDictionary(
            resource => resource.ResourceId,
            resource => resource.Name) ?? new Dictionary<ResourceId, string>();
        var needNames = simulation?.Needs?.ToDictionary(
            need => need.NeedId,
            need => need.Name) ?? new Dictionary<NeedId, string>();
        var focusedLocationDefinition = simulation?.Locations?.FirstOrDefault(
            location => location.LocationId.ToString() == focusedScene);
        var focusedLocation = focusedLocationDefinition?.LocationId;
        var allCharacters = CharacterOptions(
            simulation,
            playerCharacters,
            entityNames);
        var availableFacts = simulation is null
            ? Array.Empty<FactId>()
            : AvailableFacts(simulation, eventLog.Events);
        var configuredResources = simulation?.Resources ??
            Array.Empty<ResourcePresentationDefinition>();
        var displayedNpcs = (simulation?.Npcs ??
                Array.Empty<NpcSimulationDefinition>())
            .Where(npc =>
                focusedLocation is not LocationId locationId ||
                world.GetLocation(npc.EntityId) == locationId)
            .ToArray();
        var displayedPlayers = playerCharacters
            .Where(character =>
                focusedLocation is not LocationId locationId ||
                world.GetLocation(character.EntityId) == locationId)
            .ToArray();
        var narratedEvents = focusedLocation is LocationId sceneLocation
            ? EventsInLocation(eventLog, sceneLocation, entityNames)
            : eventLog.Events;
        var narration = new DeterministicNarrator().Narrate(
            narratedEvents,
            new NarrationContext(entityNames, locationNames, resourceNames, needNames));
        var content = new StringBuilder();

        content.Append("<header class=\"hero\"><div><p class=\"eyebrow\">Tavolo del GM</p><h1>Il mondo, adesso</h1><p class=\"lede\">Una vista leggibile dello stato persistente della campagna.</p></div><div class=\"hero-actions\"><a class=\"button secondary\" href=\"/chronicle\">Cronaca completa</a><a class=\"button secondary\" href=\"/diagnostics\">Diagnostica</a><button type=\"button\" class=\"secondary\" onclick=\"location.reload()\">Aggiorna</button></div></header>");
        content.Append(CampaignControls(
            worldFile,
            actionToken,
            campaigns));
        content.Append(RenderAiGmMode(
            aiGmSettings,
            actionToken,
            aiGmRuntimeNotice));
        content.Append(RenderSceneFocus(
            simulation?.Locations ??
                Array.Empty<LocationPresentationDefinition>(),
            focusedLocationDefinition,
            displayedNpcs.Length + displayedPlayers.Length,
            world,
            actionToken));
        content.Append(RenderSceneCastActions(
            allCharacters,
            simulation?.Locations ??
                Array.Empty<LocationPresentationDefinition>(),
            focusedLocationDefinition,
            world,
            actionToken));
        content.Append(RenderSceneConsequences(
            allCharacters,
            configuredResources,
            availableFacts,
            focusedLocationDefinition,
            world,
            actionToken));
        content.Append(RenderAiGmConfirmationQueue(
            eventLog,
            entityNames,
            locationNames,
            resourceNames,
            actionToken));
        content.Append(RenderPlayerActionQueue(
            eventLog,
            playerCharacters,
            actionToken,
            aiGmSettings,
            aiGmActionsInProgress));
        var toolboxClass = pendingAdvance is null
            ? "gm-toolbox"
            : "gm-toolbox has-preview";
        content.Append($"<details id=\"gm-toolbox\" class=\"{toolboxClass}\" open><summary><span>Strumenti completi del GM</span><small>Personaggi, tempo e conseguenze avanzate</small></summary><div class=\"gm-toolbox-content\">");
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Compagnia</p><h2>Personaggi giocanti</h2><p>Aggiungi un personaggio controllato dai giocatori. Il motore non prenderà decisioni al suo posto.</p></div><form method=\"post\" action=\"/player-character\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"player-name\">Nome</label><input id=\"player-name\" name=\"name\" maxlength=\"80\" placeholder=\"Arianna\" required><button type=\"submit\">Aggiungi personaggio</button></form></section>");
        content.Append("<section class=\"time-control\"><div><p class=\"eyebrow\">Simulazione</p><h2>Avanza il tempo</h2><p>Calcola ciò che il villaggio farebbe, senza modificare subito il salvataggio.</p></div><form method=\"post\" action=\"/advance\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"hours\">Intervallo</label><select id=\"hours\" name=\"hours\"><option value=\"1\">1 ora</option><option value=\"6\">6 ore</option><option value=\"24\">1 giorno</option></select><button type=\"submit\">Mostra anteprima</button></form></section>");
        if (pendingAdvance is not null && Path.GetFullPath(worldFile).Equals(
            pendingAdvance.WorldFile,
            StringComparison.OrdinalIgnoreCase))
        {
            var proposedNarration = new DeterministicNarrator().Narrate(
                eventLog.Events.Concat(pendingAdvance.Events),
                new NarrationContext(
                    entityNames,
                    locationNames,
                    resourceNames,
                    needNames))
                .TakeLast(pendingAdvance.Events.Count)
                .ToArray();
            content.Append("<section class=\"consequence-preview\"><div><p class=\"eyebrow\">Anteprima</p><h2>Conseguenze proposte</h2>");
            content.Append($"<p>Il mondo raggiungerà <strong>{Encode(pendingAdvance.TargetTime.ToString("dd MMM yyyy · HH:mm"))}</strong>. Nulla è ancora stato salvato.</p></div><ol>");
            foreach (var line in proposedNarration)
            {
                content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM · HH:mm"))}</time><span>{Encode(line.Text)}</span></li>");
            }
            content.Append("</ol><div class=\"preview-actions\"><form method=\"post\" action=\"/advance/approve\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><button type=\"submit\">Approva e salva</button></form><form method=\"post\" action=\"/advance/reject\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><button type=\"submit\" class=\"secondary\">Annulla</button></form></div></section>");
        }
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Intervento del GM</p><h2>Sposta un personaggio</h2><p>Registra uno spostamento esterno all'ora attuale del mondo.</p></div><form method=\"post\" action=\"/move\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"entity\">Personaggio</label><select id=\"entity\" name=\"entity\">");
        foreach (var npc in simulation?.Npcs ??
            Array.Empty<NpcSimulationDefinition>())
        {
            var name = entityNames.GetValueOrDefault(
                npc.EntityId,
                npc.EntityId.ToString());
            content.Append($"<option value=\"{Encode(npc.EntityId.ToString())}\">{Encode(name)}</option>");
        }
        foreach (var character in playerCharacters)
        {
            content.Append($"<option value=\"{Encode(character.EntityId.ToString())}\">{Encode(character.Name)} (PG)</option>");
        }

        content.Append("</select><label for=\"location\">Destinazione</label><select id=\"location\" name=\"location\">");
        foreach (var location in simulation?.Locations ??
            Array.Empty<LocationPresentationDefinition>())
        {
            content.Append($"<option value=\"{Encode(location.LocationId.ToString())}\">{Encode(location.Name)}</option>");
        }

        content.Append("</select><button type=\"submit\">Registra spostamento</button></form></section>");
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Informazioni</p><h2>Rivela una conoscenza</h2><p>Fai apprendere a un personaggio un fatto già definito nel mondo.</p></div><form method=\"post\" action=\"/reveal\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"knowledge-entity\">Personaggio</label><select id=\"knowledge-entity\" name=\"entity\">");
        foreach (var npc in simulation?.Npcs ??
            Array.Empty<NpcSimulationDefinition>())
        {
            var name = entityNames.GetValueOrDefault(
                npc.EntityId,
                npc.EntityId.ToString());
            content.Append($"<option value=\"{Encode(npc.EntityId.ToString())}\">{Encode(name)}</option>");
        }

        foreach (var character in playerCharacters)
        {
            content.Append($"<option value=\"{Encode(character.EntityId.ToString())}\">{Encode(character.Name)} (PG)</option>");
        }

        content.Append("</select><label for=\"fact\">Informazione</label><select id=\"fact\" name=\"fact\">");
        foreach (var factId in availableFacts)
        {
            content.Append($"<option value=\"{Encode(factId.ToString())}\">{Encode(factId.ToString())}</option>");
        }

        content.Append("</select><label for=\"new-fact\">Oppure nuovo ID</label><input id=\"new-fact\" name=\"newFact\" maxlength=\"100\" placeholder=\"village:bandits-seen\"><button type=\"submit\">Rivela informazione</button></form></section>");
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Conseguenze</p><h2>Trasferisci monete</h2><p>Registra un passaggio di denaro motivato tra due personaggi.</p></div><form method=\"post\" action=\"/coins/transfer\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"coin-payer\">Da</label><select id=\"coin-payer\" name=\"payer\">");
        foreach (var entity in allCharacters)
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"coin-payee\">A</label><select id=\"coin-payee\" name=\"payee\">");
        foreach (var entity in allCharacters)
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"coin-amount\">Monete</label><input id=\"coin-amount\" name=\"amount\" type=\"number\" min=\"1\" max=\"1000000\" value=\"1\" required><label for=\"coin-reason\">Motivazione</label><input id=\"coin-reason\" name=\"reason\" maxlength=\"200\" placeholder=\"Ricompensa per l'incarico\" required><button type=\"submit\">Registra trasferimento</button></form></section>");
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Conseguenze</p><h2>Gestisci una risorsa</h2><p>Registra un'acquisizione, una perdita o un trasferimento motivato.</p></div><form method=\"post\" action=\"/resources/change\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"resource-operation\">Operazione</label><select id=\"resource-operation\" name=\"operation\"><option value=\"acquire\">Acquisisce</option><option value=\"lose\">Perde</option><option value=\"transfer\">Trasferisce</option></select><label for=\"resource-entity\">Personaggio</label><select id=\"resource-entity\" name=\"entity\">");
        foreach (var entity in allCharacters)
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"resource-destination\">Destinatario</label><select id=\"resource-destination\" name=\"destination\">");
        foreach (var entity in allCharacters)
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"resource-kind\">Risorsa</label><select id=\"resource-kind\" name=\"resource\">");
        foreach (var resource in simulation?.Resources ?? Array.Empty<ResourcePresentationDefinition>())
        {
            content.Append($"<option value=\"{Encode(resource.ResourceId.ToString())}\">{Encode(resource.Name)}</option>");
        }
        content.Append("</select><label for=\"resource-quantity\">Quantità</label><input id=\"resource-quantity\" name=\"quantity\" type=\"number\" min=\"1\" max=\"1000000\" value=\"1\" required><label for=\"resource-reason\">Motivazione</label><input id=\"resource-reason\" name=\"reason\" maxlength=\"200\" placeholder=\"Trovato durante l'esplorazione\" required><button type=\"submit\">Applica conseguenza</button></form></section>");
        content.Append("<section class=\"world-action player-action\"><div><p class=\"eyebrow\">Personaggi giocanti</p><h2>Registra un'azione</h2><p>Annota ciò che un personaggio giocante ha fatto. Le conseguenze sul mondo si applicano con gli interventi specifici.</p></div><form method=\"post\" action=\"/player-action\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"player-actor\">Personaggio</label><select id=\"player-actor\" name=\"actor\" required>");
        foreach (var character in playerCharacters)
        {
            content.Append($"<option value=\"{Encode(character.EntityId.ToString())}\">{Encode(character.Name)}</option>");
        }
        content.Append("</select><label for=\"player-description\">Azione</label><textarea id=\"player-description\" name=\"description\" maxlength=\"500\" rows=\"3\" placeholder=\"Convince la guardia ad aprire il cancello.\" required></textarea><button type=\"submit\">Registra nella cronaca</button></form></section></div></details>");
        content.Append("<main><section class=\"world-strip\">");
        content.Append(Metric("Ora del mondo", world.CurrentTime.ToString("dd MMM yyyy · HH:mm")));
        content.Append(Metric("Clima", WeatherLabel(world.Weather)));
        content.Append(Metric("Eventi registrati", eventLog.Events.Count.ToString()));
        content.Append(Metric(
            focusedLocation is null ? "Personaggi attivi" : "Presenti nella scena",
            (displayedNpcs.Length + displayedPlayers.Length).ToString()));
        content.Append("</section><section id=\"characters\"><div class=\"section-heading\"><p class=\"eyebrow\">Presenze</p><h2>");
        content.Append(focusedLocationDefinition is null
            ? "Personaggi"
            : $"Personaggi in {Encode(focusedLocationDefinition.Name)}");
        content.Append("</h2></div><div class=\"npc-grid\">");
        if (displayedNpcs.Length + displayedPlayers.Length == 0)
        {
            content.Append("<p class=\"empty-copy\">Nessun personaggio è presente in questa scena.</p>");
        }

        foreach (var npc in displayedNpcs)
        {
            var name = entityNames.GetValueOrDefault(npc.EntityId, npc.EntityId.ToString());
            var location = world.GetLocation(npc.EntityId);
            var locationName = location is LocationId locationId
                ? locationNames.GetValueOrDefault(locationId, locationId.ToString())
                : "Fuori scena";
            var knownFacts = KnownFacts(npc, eventLog.Events);

            content.Append("<article class=\"npc-card\"><div class=\"npc-top\"><div>");
            content.Append($"<p class=\"role\">{Encode(npc.Role)}</p><h3>{Encode(name)}</h3></div>");
            content.Append($"<span class=\"location\">{Encode(locationName)}</span></div><div class=\"stats\">");
            content.Append(Stat("Monete", world.GetBalance(npc.EntityId).ToString()));

            foreach (var resource in simulation?.Resources ?? Array.Empty<ResourcePresentationDefinition>())
            {
                var quantity = world.GetResourceQuantity(npc.EntityId, resource.ResourceId);
                if (quantity > 0)
                {
                    content.Append(Stat(resource.Name, quantity.ToString()));
                }
            }

            foreach (var needId in (npc.DailyNeedIncreases ?? Array.Empty<DailyNeedIncreaseDefinition>()).Select(increase => increase.NeedId).Distinct())
            {
                content.Append(Stat(
                    needNames.GetValueOrDefault(needId, needId.ToString()),
                    $"{world.GetNeedLevel(npc.EntityId, needId)}/100"));
            }

            content.Append("</div><div class=\"knowledge\"><span>Conoscenze</span>");
            content.Append(knownFacts.Count == 0
                ? "<p>Nessuna informazione nota.</p>"
                : $"<p>{string.Join(" · ", knownFacts.Select(fact => Encode(fact.ToString())))}</p>");
            content.Append("</div></article>");
        }

        foreach (var character in displayedPlayers)
        {
            var location = world.GetLocation(character.EntityId);
            var locationName = location is LocationId locationId
                ? locationNames.GetValueOrDefault(locationId, locationId.ToString())
                : "Fuori scena";
            var knownFacts = KnownFacts(character.EntityId, eventLog.Events);

            content.Append("<article class=\"npc-card player-card\"><div class=\"npc-top\"><div>");
            content.Append($"<p class=\"role\">personaggio giocante</p><h3>{Encode(character.Name)}</h3></div>");
            content.Append($"<span class=\"location\">{Encode(locationName)}</span></div><div class=\"stats\">");
            content.Append(Stat("Monete", world.GetBalance(character.EntityId).ToString()));
            foreach (var resource in simulation?.Resources ?? Array.Empty<ResourcePresentationDefinition>())
            {
                var quantity = world.GetResourceQuantity(character.EntityId, resource.ResourceId);
                if (quantity > 0)
                {
                    content.Append(Stat(resource.Name, quantity.ToString()));
                }
            }
            content.Append("</div><div class=\"knowledge\"><span>Conoscenze</span>");
            content.Append(knownFacts.Count == 0
                ? "<p>Nessuna informazione nota.</p>"
                : $"<p>{string.Join(" · ", knownFacts.Select(fact => Encode(fact.ToString())))}</p>");
            content.Append("</div>");
            content.Append("<form class=\"player-access-form\" method=\"post\" action=\"/player-access\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
            content.Append($"<input type=\"hidden\" name=\"entity\" value=\"{Encode(character.EntityId.ToString())}\">");
            content.Append("<button class=\"secondary\" type=\"submit\">Genera codice giocatore</button></form>");
            content.Append($"<a class=\"player-view-link\" href=\"/player/{Uri.EscapeDataString(character.EntityId.ToString())}\">Anteprima vista giocatore</a></article>");
        }

        content.Append("</div></section><section id=\"world-log\" class=\"chronicle\"><div class=\"section-heading\"><p class=\"eyebrow\">Registro</p><h2>");
        content.Append(focusedLocationDefinition is null
            ? "Ultimi avvenimenti"
            : $"Cronaca di {Encode(focusedLocationDefinition.Name)}");
        content.Append("</h2></div><ol>");
        foreach (var line in narration.TakeLast(12).Reverse())
        {
            content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM · HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
        }

        if (narration.Count == 0)
        {
            content.Append("<li><p>Nessun avvenimento registrato per questa scena.</p></li>");
        }

        content.Append("</ol></section></main><nav class=\"gm-mobile-nav\" aria-label=\"Navigazione del Tavolo del GM\"><a href=\"#scene-focus\">Scena</a><a href=\"#gm-actions\">Azioni</a><a href=\"#characters\">Presenti</a><a href=\"#world-log\">Registro</a></nav><footer><span>Salvataggio</span>");
        content.Append($"<code>{Encode(worldFile)}</code></footer>");
        content.Append("<script>(()=>{const box=document.getElementById('gm-toolbox');if(box&&window.matchMedia('(max-width:800px)').matches&&!box.classList.contains('has-preview'))box.open=false;})();</script>");
        if ((eventLog.PlayerActions ?? []).Any(action =>
            aiGmActionsInProgress.Contains(action.Id)))
        {
            content.Append("<script>window.setTimeout(()=>location.reload(),2500);</script>");
        }
        return Page("Tavolo del GM", content.ToString());
    }

    private static string RenderSceneFocus(
        IReadOnlyList<LocationPresentationDefinition> locations,
        LocationPresentationDefinition? focusedLocation,
        int presentCharacters,
        WorldSnapshot world,
        string actionToken)
    {
        if (locations.Count == 0)
        {
            return string.Empty;
        }

        var content = new StringBuilder();
        content.Append("<section id=\"scene-focus\" class=\"scene-focus\"><div><p class=\"eyebrow\">Scena del GM</p>");
        if (focusedLocation is null)
        {
            content.Append("<h2>Vista globale</h2><p>Stai osservando tutti i luoghi e l'intera cronaca del mondo.</p>");
        }
        else
        {
            content.Append($"<h2>{Encode(focusedLocation.Name)}</h2><p><strong>{presentCharacters}</strong> personaggi presenti · {Encode(WeatherLabel(world.Weather))} · {Encode(world.CurrentTime.ToString("HH:mm"))}. Schede e cronaca sono filtrate su questo luogo.</p>");
        }

        content.Append("</div><form method=\"post\" action=\"/scene/focus\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><label for=\"focused-scene\">Luogo in primo piano</label><select id=\"focused-scene\" name=\"scene\">");
        content.Append($"<option value=\"\"{(focusedLocation is null ? " selected" : string.Empty)}>Tutto il mondo</option>");
        foreach (var location in locations.OrderBy(
            location => location.Name,
            StringComparer.OrdinalIgnoreCase))
        {
            var selected = focusedLocation?.LocationId == location.LocationId
                ? " selected"
                : string.Empty;
            content.Append($"<option value=\"{Encode(location.LocationId.ToString())}\"{selected}>{Encode(location.Name)}</option>");
        }

        content.Append("</select><button type=\"submit\">Metti a fuoco</button></form></section>");
        return content.ToString();
    }

    private static string RenderSceneCastActions(
        IReadOnlyList<(EntityId Id, string Name)> characters,
        IReadOnlyList<LocationPresentationDefinition> locations,
        LocationPresentationDefinition? focusedLocation,
        WorldSnapshot world,
        string actionToken)
    {
        if (focusedLocation is null)
        {
            return string.Empty;
        }

        var present = characters
            .Where(character =>
                world.GetLocation(character.Id) == focusedLocation.LocationId)
            .ToArray();
        var outside = characters
            .Where(character =>
                world.GetLocation(character.Id) != focusedLocation.LocationId)
            .ToArray();
        var destinations = locations
            .Where(location =>
                location.LocationId != focusedLocation.LocationId)
            .OrderBy(location =>
                location.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var content = new StringBuilder();
        content.Append("<section class=\"scene-cast\"><div class=\"section-heading\"><p class=\"eyebrow\">Azioni rapide</p><h2>Organizza i presenti</h2></div><div class=\"scene-cast-grid\">");

        if (outside.Length == 0)
        {
            content.Append("<div class=\"scene-cast-empty\"><h3>Porta in scena</h3><p>Tutti i personaggi sono già presenti.</p></div>");
        }
        else
        {
            content.Append("<form method=\"post\" action=\"/move\"><h3>Porta in scena</h3>");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"location\" value=\"{Encode(focusedLocation.LocationId.ToString())}\"><label for=\"scene-add-character\">Personaggio</label><select id=\"scene-add-character\" name=\"entity\">");
            foreach (var character in outside)
            {
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\">{Encode(character.Name)}</option>");
            }

            content.Append("</select><button type=\"submit\">Porta qui</button></form>");
        }

        if (present.Length == 0 || destinations.Length == 0)
        {
            content.Append("<div class=\"scene-cast-empty\"><h3>Sposta altrove</h3><p>Non ci sono spostamenti disponibili.</p></div>");
        }
        else
        {
            content.Append("<form method=\"post\" action=\"/move\"><h3>Sposta altrove</h3>");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><label for=\"scene-remove-character\">Personaggio</label><select id=\"scene-remove-character\" name=\"entity\">");
            foreach (var character in present)
            {
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\">{Encode(character.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-destination\">Destinazione</label><select id=\"scene-destination\" name=\"location\">");
            foreach (var destination in destinations)
            {
                content.Append($"<option value=\"{Encode(destination.LocationId.ToString())}\">{Encode(destination.Name)}</option>");
            }

            content.Append("</select><button type=\"submit\" class=\"secondary\">Sposta</button></form>");
        }

        content.Append("</div></section>");
        return content.ToString();
    }

    private static string RenderSceneConsequences(
        IReadOnlyList<(EntityId Id, string Name)> characters,
        IReadOnlyList<ResourcePresentationDefinition> resources,
        IReadOnlyList<FactId> availableFacts,
        LocationPresentationDefinition? focusedLocation,
        WorldSnapshot world,
        string actionToken)
    {
        if (focusedLocation is null)
        {
            return string.Empty;
        }

        var present = characters
            .Where(character =>
                world.GetLocation(character.Id) == focusedLocation.LocationId)
            .ToArray();
        var content = new StringBuilder();
        content.Append("<section class=\"scene-consequences\"><div class=\"section-heading\"><p class=\"eyebrow\">Conseguenze rapide</p><h2>Agisci nella scena</h2></div><div class=\"scene-consequence-grid\">");

        if (present.Length < 2)
        {
            content.Append("<div class=\"scene-cast-empty\"><h3>Monete</h3><p>Servono almeno due personaggi presenti.</p></div>");
        }
        else
        {
            content.Append("<form method=\"post\" action=\"/coins/transfer\"><h3>Trasferisci monete</h3>");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><label for=\"scene-coin-payer\">Da</label><select id=\"scene-coin-payer\" name=\"payer\">");
            foreach (var character in present)
            {
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\">{Encode(character.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-coin-payee\">A</label><select id=\"scene-coin-payee\" name=\"payee\">");
            for (var index = 0; index < present.Length; index++)
            {
                var character = present[index];
                var selected = index == 1 ? " selected" : string.Empty;
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\"{selected}>{Encode(character.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-coin-amount\">Monete</label><input id=\"scene-coin-amount\" name=\"amount\" type=\"number\" min=\"1\" max=\"1000000\" value=\"1\" required><label for=\"scene-coin-reason\">Motivazione</label><input id=\"scene-coin-reason\" name=\"reason\" maxlength=\"200\" placeholder=\"Pagamento o ricompensa\" required><button type=\"submit\">Trasferisci</button></form>");
        }

        if (present.Length == 0 || resources.Count == 0)
        {
            content.Append("<div class=\"scene-cast-empty\"><h3>Risorse</h3><p>Non ci sono personaggi o risorse disponibili.</p></div>");
        }
        else
        {
            content.Append("<form method=\"post\" action=\"/resources/change\"><h3>Modifica una risorsa</h3>");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"destination\" value=\"\"><label for=\"scene-resource-operation\">Operazione</label><select id=\"scene-resource-operation\" name=\"operation\"><option value=\"acquire\">Acquisisce</option><option value=\"lose\">Perde</option></select><label for=\"scene-resource-character\">Personaggio</label><select id=\"scene-resource-character\" name=\"entity\">");
            foreach (var character in present)
            {
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\">{Encode(character.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-resource-kind\">Risorsa</label><select id=\"scene-resource-kind\" name=\"resource\">");
            foreach (var resource in resources.OrderBy(
                resource => resource.Name,
                StringComparer.OrdinalIgnoreCase))
            {
                content.Append($"<option value=\"{Encode(resource.ResourceId.ToString())}\">{Encode(resource.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-resource-quantity\">Quantità</label><input id=\"scene-resource-quantity\" name=\"quantity\" type=\"number\" min=\"1\" max=\"1000000\" value=\"1\" required><label for=\"scene-resource-reason\">Motivazione</label><input id=\"scene-resource-reason\" name=\"reason\" maxlength=\"200\" placeholder=\"Trovata o consumata nella scena\" required><button type=\"submit\">Applica</button></form>");
        }

        if (present.Length == 0)
        {
            content.Append("<div class=\"scene-cast-empty\"><h3>Conoscenze</h3><p>Non ci sono personaggi presenti.</p></div>");
        }
        else
        {
            content.Append("<form method=\"post\" action=\"/reveal\"><h3>Rivela una conoscenza</h3>");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><label for=\"scene-knowledge-character\">Personaggio</label><select id=\"scene-knowledge-character\" name=\"entity\">");
            foreach (var character in present)
            {
                content.Append($"<option value=\"{Encode(character.Id.ToString())}\">{Encode(character.Name)}</option>");
            }

            content.Append("</select><label for=\"scene-known-fact\">Informazione esistente</label><select id=\"scene-known-fact\" name=\"fact\"><option value=\"\">Nuova informazione</option>");
            foreach (var factId in availableFacts)
            {
                content.Append($"<option value=\"{Encode(factId.ToString())}\">{Encode(factId.ToString())}</option>");
            }

            content.Append("</select><label for=\"scene-new-fact\">Oppure nuovo ID</label><input id=\"scene-new-fact\" name=\"newFact\" maxlength=\"100\" placeholder=\"village:secret-discovered\"><button type=\"submit\">Rivela</button></form>");
        }

        content.Append("</div></section>");
        return content.ToString();
    }

    private static IReadOnlyList<IWorldEvent> EventsInLocation(
        WorldEventLog eventLog,
        LocationId locationId,
        IReadOnlyDictionary<EntityId, string> entityNames)
    {
        var balances = eventLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var world = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            balances,
            eventLog.InitialWorld.ResourceStocks ??
                Array.Empty<EntityResourceStock>(),
            eventLog.InitialWorld.Weather);
        var processor = new WorldEventProcessor();
        var localEvents = new List<IWorldEvent>();

        foreach (var worldEvent in eventLog.Events)
        {
            if (OccursInLocation(
                world,
                worldEvent,
                locationId,
                entityNames))
            {
                localEvents.Add(worldEvent);
            }

            world = processor.Apply(world, worldEvent);
        }

        return localEvents;
    }

    private static bool OccursInLocation(
        WorldSnapshot world,
        IWorldEvent worldEvent,
        LocationId locationId,
        IReadOnlyDictionary<EntityId, string> entityNames) =>
        worldEvent switch
        {
            WeatherChanged => true,
            EntityEnteredLocation entered =>
                entered.LocationId == locationId,
            EntityLeftLocation left => left.LocationId == locationId,
            ResourceProduced produced => produced.LocationId == locationId,
            PlayerActionRecorded action => entityNames.Any(entity =>
                entity.Value.Equals(
                    action.Actor,
                    StringComparison.OrdinalIgnoreCase) &&
                world.GetLocation(entity.Key) == locationId),
            _ => EventEntities(world, worldEvent).Any(entityId =>
                world.GetLocation(entityId) == locationId)
        };

    private static IReadOnlyList<EntityId> EventEntities(
        WorldSnapshot world,
        IWorldEvent worldEvent) =>
        worldEvent switch
        {
            OrderRequested requested =>
                new[] { requested.CustomerId, requested.ArtisanId },
            OrderAccepted accepted =>
                OrderParticipants(world, accepted.OrderId),
            PaymentTransferred payment =>
                new[] { payment.PayerId, payment.PayeeId },
            CoinsTransferred coins =>
                new[] { coins.PayerId, coins.PayeeId },
            OrderWorkStarted started =>
                OrderParticipants(world, started.OrderId),
            OrderCompleted completed =>
                OrderParticipants(world, completed.OrderId),
            OrderDelivered delivered =>
                OrderParticipants(world, delivered.OrderId),
            FactShared shared =>
                new[] { shared.SpeakerId, shared.ListenerId },
            FactRevealed revealed => new[] { revealed.EntityId },
            TrustChanged trust =>
                new[] { trust.SubjectId, trust.OtherEntityId },
            TradeCompleted trade =>
                new[] { trade.BuyerId, trade.SellerId },
            NeedIncreased need => new[] { need.EntityId },
            ResourceConsumed consumed => new[] { consumed.EntityId },
            ResourceAcquired acquired => new[] { acquired.EntityId },
            ResourceLost lost => new[] { lost.EntityId },
            ResourceTransferred transferred =>
                new[] { transferred.SourceId, transferred.DestinationId },
            PlayerCharacterRegistered player => new[] { player.EntityId },
            _ => Array.Empty<EntityId>()
        };

    private static IReadOnlyList<EntityId> OrderParticipants(
        WorldSnapshot world,
        OrderId orderId) =>
        world.GetOrder(orderId) is { } order
            ? new[] { order.CustomerId, order.ArtisanId }
            : Array.Empty<EntityId>();

    private static string RenderAiGmMode(
        AiGmCampaignModeSettings settings,
        string actionToken,
        string? runtimeNotice)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var enabledLabel = settings.Enabled
            ? "Modalità AI selezionata"
            : "GM umano";
        var ollamaConfigured = settings.ProviderConfigured &&
            settings.ProviderId!.Equals(
                "ollama",
                StringComparison.OrdinalIgnoreCase);
        var providerLabel = ollamaConfigured
            ? $"Ollama locale · {settings.Model}"
            : settings.ProviderConfigured
                ? $"{settings.ProviderId} · {settings.Model}"
                : "Ollama non ancora configurato";
        var explanation = settings.Enabled
            ? ollamaConfigured
                ? "Ogni nuova azione umana viene affidata a Ollama. Le conseguenze importanti attendono comunque la tua conferma."
                : "La modalità è predisposta, ma le azioni restano al GM umano finché Ollama non viene configurato."
            : "Tessitore non invia azioni a servizi IA. Memoria e regole restano comunque nei salvataggi locali.";
        var nextEnabled = settings.Enabled ? "false" : "true";
        var buttonLabel = settings.Enabled
            ? "Torna al GM umano"
            : "Seleziona modalità AI";
        var buttonClass = settings.Enabled ? "secondary" : string.Empty;

        var model = ollamaConfigured
            ? settings.Model!
            : "qwen2.5:7b";
        var notice = string.IsNullOrWhiteSpace(runtimeNotice)
            ? string.Empty
            : $"<p class=\"ai-runtime-notice\">{Encode(runtimeNotice)}</p>";

        return "<section id=\"ai-gm-mode\" class=\"ai-mode-panel\">" +
            "<div><p class=\"eyebrow\">Modalità di conduzione</p>" +
            $"<h2>{Encode(enabledLabel)}</h2>" +
            $"<p>{Encode(explanation)}</p>" +
            $"<span class=\"ai-provider-status\">{Encode(providerLabel)}</span>" +
            notice +
            "</div><div class=\"ai-mode-actions\"><form class=\"ai-model-form\" method=\"post\" action=\"/ai-gm/ollama\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">" +
            "<label for=\"ollama-model\">Modello Ollama installato</label>" +
            $"<input id=\"ollama-model\" name=\"model\" value=\"{Encode(model)}\" maxlength=\"100\" required>" +
            "<button type=\"submit\" class=\"secondary\">Configura</button></form>" +
            "<form method=\"post\" action=\"/ai-gm/mode\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">" +
            $"<input type=\"hidden\" name=\"enabled\" value=\"{nextEnabled}\">" +
            $"<button type=\"submit\" class=\"{buttonClass}\">{Encode(buttonLabel)}</button>" +
            "</form></div></section>";
    }

    private static string RenderAiGmConfirmationQueue(
        WorldEventLog eventLog,
        IReadOnlyDictionary<EntityId, string> entityNames,
        IReadOnlyDictionary<LocationId, string> locationNames,
        IReadOnlyDictionary<ResourceId, string> resourceNames,
        string actionToken)
    {
        var pending = new AiGmTurnCoordinator()
            .PendingConsequences(eventLog);
        if (pending.Count == 0)
        {
            return string.Empty;
        }

        var actions = (eventLog.PlayerActions ??
                Array.Empty<PlayerActionProposal>())
            .ToDictionary(action => action.Id);
        var content = new StringBuilder();
        content.Append("<section id=\"ai-confirmations\" class=\"ai-confirmation-queue\"><div class=\"section-heading\"><p class=\"eyebrow\">Game Master AI</p><h2>Conseguenze importanti</h2><p>Queste modifiche sono salvate, ma non sono ancora attive nel mondo.</p></div><div class=\"ai-consequence-list\">");

        foreach (var item in pending)
        {
            var playerName = entityNames.GetValueOrDefault(
                item.PlayerCharacterId,
                item.PlayerCharacterId.ToString());
            var playerAction = actions.GetValueOrDefault(item.PlayerActionId);
            content.Append("<article class=\"ai-consequence-item\"><div class=\"ai-consequence-copy\">");
            content.Append($"<span class=\"action-status\">{Encode(playerName)} · Conferma richiesta</span>");
            if (playerAction is not null)
            {
                content.Append($"<p><strong>Azione del giocatore:</strong> {Encode(playerAction.Description)}</p>");
            }
            content.Append($"<p class=\"ai-narration\">{Encode(item.Narration)}</p>");
            content.Append($"<div class=\"ai-proposed-change\"><strong>{Encode(DescribeAiConsequence(item.Consequence, entityNames, locationNames, resourceNames))}</strong> <small>{Encode(item.Consequence.AssessmentReason)}</small></div></div>");
            content.Append("<form method=\"post\" action=\"/ai-gm/consequences/resolve\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"consequenceId\" value=\"{item.Consequence.Id}\">");
            content.Append("<label>Nota facoltativa<textarea name=\"resolution\" maxlength=\"500\" rows=\"2\" placeholder=\"Motivo della decisione\"></textarea></label><div class=\"resolution-buttons\"><button name=\"decision\" value=\"approve\" type=\"submit\">Approva</button><button name=\"decision\" value=\"reject\" type=\"submit\" class=\"secondary\">Rifiuta</button></div></form></article>");
        }

        content.Append("</div></section>");
        return content.ToString();
    }

    private static string DescribeAiConsequence(
        AiGmConsequenceRecord consequence,
        IReadOnlyDictionary<EntityId, string> entityNames,
        IReadOnlyDictionary<LocationId, string> locationNames,
        IReadOnlyDictionary<ResourceId, string> resourceNames)
    {
        string Entity(EntityId? id) => id is { } value
            ? entityNames.GetValueOrDefault(value, value.ToString())
            : "entità sconosciuta";
        string Location(LocationId? id) => id is { } value
            ? locationNames.GetValueOrDefault(value, value.ToString())
            : "luogo sconosciuto";
        string Resource(ResourceId? id) => id is { } value
            ? resourceNames.GetValueOrDefault(value, value.ToString())
            : "risorsa sconosciuta";

        return consequence.Kind switch
        {
            AiGmConsequenceKind.MoveEntity =>
                $"Sposta {Entity(consequence.EntityId)} in " +
                $"{Location(consequence.LocationId)}.",
            AiGmConsequenceKind.TransferCoins =>
                $"Trasferisce {consequence.Amount} monete da " +
                $"{Entity(consequence.EntityId)} a " +
                $"{Entity(consequence.OtherEntityId)}.",
            AiGmConsequenceKind.AcquireResource =>
                $"{Entity(consequence.EntityId)} acquisisce " +
                $"{consequence.Amount} × {Resource(consequence.ResourceId)}.",
            AiGmConsequenceKind.LoseResource =>
                $"{Entity(consequence.EntityId)} perde " +
                $"{consequence.Amount} × {Resource(consequence.ResourceId)}.",
            AiGmConsequenceKind.TransferResource =>
                $"Trasferisce {consequence.Amount} × " +
                $"{Resource(consequence.ResourceId)} da " +
                $"{Entity(consequence.EntityId)} a " +
                $"{Entity(consequence.OtherEntityId)}.",
            AiGmConsequenceKind.RevealFact =>
                $"Rivela {consequence.FactId} a " +
                $"{Entity(consequence.EntityId)}.",
            AiGmConsequenceKind.ChangeTrust =>
                $"Modifica di {consequence.Amount:+#;-#;0} la fiducia di " +
                $"{Entity(consequence.EntityId)} verso " +
                $"{Entity(consequence.OtherEntityId)}.",
            _ => "Conseguenza non riconosciuta."
        };
    }

    private static string RenderPlayerActionQueue(
        WorldEventLog eventLog,
        IReadOnlyList<PlayerCharacterRegistered> playerCharacters,
        string actionToken,
        AiGmCampaignModeSettings aiGmSettings,
        IReadOnlySet<Guid> aiGmActionsInProgress)
    {
        var aiManagedActions = (eventLog.AiGmTurns ??
                Array.Empty<AiGmTurnRecord>())
            .Where(turn => turn.Status == AiGmTurnStatus.PendingConfirmation)
            .Select(turn => turn.PlayerActionId)
            .ToHashSet();
        var activeActions = (eventLog.PlayerActions ??
                Array.Empty<PlayerActionProposal>())
            .Where(action => action.Status is
                PlayerActionStatus.Pending or
                PlayerActionStatus.RollRequested or
                PlayerActionStatus.Rolled)
            .Where(action => !aiManagedActions.Contains(action.Id))
            .OrderBy(action => action.SubmittedAt)
            .ToArray();
        var names = playerCharacters.ToDictionary(
            character => character.EntityId,
            character => character.Name);
        var content = new StringBuilder();
        content.Append("<section id=\"gm-actions\" class=\"gm-action-queue\"><div class=\"section-heading\"><p class=\"eyebrow\">Giocatori</p><h2>Azioni in attesa</h2></div>");
        if (activeActions.Length == 0)
        {
            content.Append("<div class=\"player-panel\"><p>Nessuna azione attende una decisione.</p></div></section>");
            return content.ToString();
        }
        content.Append("<div class=\"gm-action-list\">");
        foreach (var action in activeActions)
        {
            var isAiProcessing = aiGmActionsInProgress.Contains(action.Id);
            var playerName = names.GetValueOrDefault(
                action.PlayerCharacterId,
                action.PlayerCharacterId.ToString());
            content.Append("<article class=\"gm-action-item\"><div class=\"gm-action-copy\">");
            content.Append($"<span class=\"action-status\">{Encode(playerName)} · {Encode(PlayerActionStatusLabel(action.Status))}</span><p>{Encode(action.Description)}</p>");
            if (action.Roll is not null)
            {
                content.Append($"<div class=\"roll-summary\">{Encode(RollSummary(action.Roll, includeSecretDifficulty: true))}</div>");
            }
            if (isAiProcessing)
            {
                content.Append("<div class=\"ai-processing\"><i></i><strong>Ollama sta elaborando</strong><p>Il tavolo si aggiornerà automaticamente.</p></div></div></article>");
                continue;
            }
            var canRetryWithOllama =
                action.Status is (PlayerActionStatus.Pending or
                    PlayerActionStatus.Rolled) &&
                aiGmSettings.Enabled &&
                aiGmSettings.ProviderConfigured &&
                aiGmSettings.ProviderId!.Equals(
                    "ollama",
                    StringComparison.OrdinalIgnoreCase) &&
                !(eventLog.AiGmTurns ?? []).Any(turn =>
                    turn.PlayerActionId == action.Id);
            if (canRetryWithOllama)
            {
                content.Append("<form class=\"ai-retry-form\" method=\"post\" action=\"/ai-gm/actions/retry\">");
                var aiButton = action.Status == PlayerActionStatus.Rolled
                    ? "Continua con Ollama"
                    : "Affida a Ollama";
                content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><button type=\"submit\" class=\"secondary\">{aiButton}</button></form>");
            }
            content.Append("</div>");
            if (action.Status == PlayerActionStatus.Pending)
            {
                content.Append("<form class=\"roll-request-form\" method=\"post\" action=\"/player-actions/request-roll\">");
                content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><label>Modalità<select name=\"mode\"><option value=\"Normal\">Normale</option><option value=\"Advantage\">Vantaggio</option><option value=\"Disadvantage\">Svantaggio</option></select></label><label>Modificatore<input name=\"modifier\" type=\"number\" min=\"-20\" max=\"20\" value=\"0\" required></label><label>Difficoltà<input name=\"difficulty\" type=\"number\" min=\"1\" max=\"40\" placeholder=\"opzionale\"></label><label class=\"checkbox-label\"><input name=\"difficultyVisible\" type=\"checkbox\"> Mostra la difficoltà</label><button type=\"submit\" class=\"secondary\">Richiedi d20</button></form>");
            }
            content.Append("<form class=\"action-resolution-form\" method=\"post\" action=\"/player-actions/resolve\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><label>Esito<textarea name=\"resolution\" maxlength=\"500\" rows=\"3\" placeholder=\"Descrivi ciò che accade.\" required></textarea></label><div class=\"resolution-buttons\"><button name=\"decision\" value=\"approve\" type=\"submit\">Approva</button><button name=\"decision\" value=\"reject\" type=\"submit\" class=\"secondary\">Rifiuta</button></div></form></article>");
        }
        content.Append("</div></section>");
        return content.ToString();
    }

    private static string RenderRollForPlayer(
        PlayerActionProposal action,
        EntityId entityId,
        string playerInteractionToken)
    {
        var roll = action.Roll!;
        var content = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(roll.PromptNarration))
        {
            content.Append($"<div class=\"action-resolution ai-player-narration\"><strong>Game Master AI</strong><p>{Encode(roll.PromptNarration)}</p></div>");
        }
        content.Append($"<div class=\"roll-summary\">{Encode(RollSummary(roll, includeSecretDifficulty: false))}</div>");
        if (action.Status == PlayerActionStatus.RollRequested)
        {
            content.Append($"<form method=\"post\" action=\"/player/{Uri.EscapeDataString(entityId.ToString())}/roll\"><input type=\"hidden\" name=\"token\" value=\"{Encode(playerInteractionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><button type=\"submit\">Tira il d20</button></form>");
        }
        return content.ToString();
    }

    private static string RollSummary(
        D20Roll roll,
        bool includeSecretDifficulty)
    {
        var mode = roll.Mode switch
        {
            D20RollMode.Advantage => "vantaggio",
            D20RollMode.Disadvantage => "svantaggio",
            _ => "tiro normale"
        };
        var difficulty = roll.Difficulty is null
            ? ""
            : roll.DifficultyVisible || includeSecretDifficulty
                ? $", difficoltà {roll.Difficulty}"
                : ", difficoltà segreta";
        var reason = string.IsNullOrWhiteSpace(roll.Reason)
            ? string.Empty
            : $" Motivo: {roll.Reason}";
        if (roll.Dice is null || roll.Total is null)
        {
            return $"d20: {mode}, modificatore {roll.Modifier:+#;-#;0}{difficulty}.{reason}";
        }
        var dice = string.Join(" e ", roll.Dice);
        var natural = roll.KeptDie is 1 or 20
            ? $" · {roll.KeptDie} naturale"
            : string.Empty;
        return $"Dadi: {dice}; risultato {roll.KeptDie} " +
            $"{roll.Modifier:+#;-#;+0} = {roll.Total}{natural}{difficulty}.{reason}";
    }

    private static string PlayerActionStatusLabel(PlayerActionStatus status) =>
        status switch
        {
            PlayerActionStatus.Pending => "In attesa del GM",
            PlayerActionStatus.RollRequested => "Tiro richiesto",
            PlayerActionStatus.Rolled => "Tiro completato",
            PlayerActionStatus.Approved => "Approvata",
            PlayerActionStatus.Rejected => "Rifiutata",
            _ => status.ToString()
        };

    private static IEnumerable<IWorldEvent> ObservableEvents(
        IReadOnlyList<IWorldEvent> events,
        PlayerCharacterRegistered character,
        LocationId? currentLocation) =>
        events.Where(worldEvent => worldEvent switch
        {
            EntityEnteredLocation entered =>
                entered.EntityId == character.EntityId ||
                entered.LocationId == currentLocation,
            EntityLeftLocation left =>
                left.EntityId == character.EntityId ||
                left.LocationId == currentLocation,
            FactShared shared =>
                shared.SpeakerId == character.EntityId ||
                shared.ListenerId == character.EntityId,
            FactRevealed revealed =>
                revealed.EntityId == character.EntityId,
            CoinsTransferred coins =>
                coins.PayerId == character.EntityId ||
                coins.PayeeId == character.EntityId,
            ResourceAcquired acquired =>
                acquired.EntityId == character.EntityId,
            ResourceLost lost => lost.EntityId == character.EntityId,
            ResourceTransferred transferred =>
                transferred.SourceId == character.EntityId ||
                transferred.DestinationId == character.EntityId,
            ResourceProduced produced =>
                produced.ProducerId == character.EntityId ||
                produced.LocationId == currentLocation,
            TradeCompleted trade =>
                trade.BuyerId == character.EntityId ||
                trade.SellerId == character.EntityId,
            TrustChanged trust =>
                trust.SubjectId == character.EntityId ||
                trust.OtherEntityId == character.EntityId,
            PlayerActionRecorded action =>
                action.Actor.Equals(
                    character.Name,
                    StringComparison.OrdinalIgnoreCase),
            WeatherChanged => true,
            _ => false
        });

    private static IReadOnlySet<FactId> KnownFacts(
        NpcSimulationDefinition npc,
        IReadOnlyList<IWorldEvent> events)
    {
        var facts = new HashSet<FactId>(npc.InitialKnownFacts ?? Array.Empty<FactId>());
        foreach (var shared in events.OfType<FactShared>().Where(shared => shared.ListenerId == npc.EntityId))
        {
            facts.Add(shared.FactId);
        }
        foreach (var revealed in events.OfType<FactRevealed>().Where(
            revealed => revealed.EntityId == npc.EntityId))
        {
            facts.Add(revealed.FactId);
        }
        return facts;
    }

    private static IReadOnlySet<FactId> KnownFacts(
        EntityId entityId,
        IReadOnlyList<IWorldEvent> events)
    {
        var facts = new HashSet<FactId>();
        foreach (var shared in events.OfType<FactShared>().Where(
            shared => shared.ListenerId == entityId))
        {
            facts.Add(shared.FactId);
        }
        foreach (var revealed in events.OfType<FactRevealed>().Where(
            revealed => revealed.EntityId == entityId))
        {
            facts.Add(revealed.FactId);
        }
        return facts;
    }

    private static IReadOnlyList<PlayerCharacterRegistered> PlayerCharacters(
        IReadOnlyList<IWorldEvent> events) =>
        events.OfType<PlayerCharacterRegistered>()
            .GroupBy(character => character.EntityId)
            .Select(group => group.Last())
            .OrderBy(character => character.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Dictionary<EntityId, string> EntityNames(
        WorldSimulationDefinition? simulation,
        IReadOnlyList<IWorldEvent> events)
    {
        var names = simulation?.Entities?.ToDictionary(
            entity => entity.EntityId,
            entity => entity.Name) ?? new Dictionary<EntityId, string>();
        foreach (var character in PlayerCharacters(events))
        {
            names[character.EntityId] = character.Name;
        }
        return names;
    }

    private static IReadOnlyList<(EntityId Id, string Name)> CharacterOptions(
        WorldSimulationDefinition? simulation,
        IReadOnlyList<PlayerCharacterRegistered> playerCharacters,
        IReadOnlyDictionary<EntityId, string> entityNames) =>
        (simulation?.Npcs ?? Array.Empty<NpcSimulationDefinition>())
            .Select(npc => (
                npc.EntityId,
                entityNames.GetValueOrDefault(
                    npc.EntityId,
                    npc.EntityId.ToString())))
            .Concat(playerCharacters.Select(character => (
                character.EntityId,
                character.Name)))
            .OrderBy(character => character.Item2, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separatorPending = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else
            {
                separatorPending = true;
            }
        }
        return builder.Length > 0 ? builder.ToString() : "personaggio";
    }

    private static FactId[] AvailableFacts(
        WorldSimulationDefinition simulation,
        IReadOnlyList<IWorldEvent> events) =>
        simulation.Npcs
            .SelectMany(npc =>
                (npc.InitialKnownFacts ?? Array.Empty<FactId>())
                .Concat((npc.KnowledgeConditionalLocations ??
                    Array.Empty<KnowledgeConditionalLocationDefinition>())
                    .Select(decision => decision.RequiredFactId))
                .Concat((npc.FactSharings ??
                    Array.Empty<FactSharingDefinition>())
                    .Select(sharing => sharing.FactId))
                .Concat((npc.TrustChangesAfterFactSharing ??
                    Array.Empty<TrustChangeAfterFactSharedDefinition>())
                    .Select(change => change.FactId)))
            .Concat(events.OfType<FactShared>().Select(shared => shared.FactId))
            .Concat(events.OfType<FactRevealed>().Select(revealed => revealed.FactId))
            .Distinct()
            .OrderBy(factId => factId.ToString(), StringComparer.Ordinal)
            .ToArray();

    private static WorldSnapshot Replay(WorldEventLog eventLog)
    {
        var balances = eventLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            balances,
            eventLog.InitialWorld.ResourceStocks ??
                Array.Empty<EntityResourceStock>(),
            eventLog.InitialWorld.Weather);
        return new WorldEventProcessor().Replay(initialWorld, eventLog.Events);
    }

    private static string RenderMissingWorld(
        string worldFile,
        string actionToken,
        IReadOnlyList<CampaignEntry> campaigns) => Page(
        "Apri un mondo",
        "<main class=\"empty-state\"><p class=\"eyebrow\">TessitoreGM</p><h1>Il tavolo è pronto.</h1><p>Seleziona una campagna disponibile oppure creane una nuova da un modello esistente.</p>" +
        CampaignControls(worldFile, actionToken, campaigns) +
        $"<p class=\"path\">File atteso: <code>{Encode(worldFile)}</code></p></main>");

    private static string RenderUnreadableWorld(
        string worldFile,
        string actionToken,
        Exception exception)
    {
        var content = new StringBuilder();
        content.Append("<main class=\"empty-state recovery-state\"><p class=\"eyebrow\">TessitoreGM</p><h1>Questa campagna non si apre.</h1>");
        content.Append($"<p>{Encode(exception.Message)}</p><code>{Encode(worldFile)}</code>");
        IReadOnlyList<WorldEventBackup> backups;
        try
        {
            backups = new WorldEventFileStore().ListBackups(worldFile);
        }
        catch (Exception backupException) when (
            backupException is IOException or ArgumentException)
        {
            content.Append($"<p>Non riesco a leggere i backup: {Encode(backupException.Message)}</p></main>");
            return Page("Mondo non leggibile", content.ToString());
        }

        if (backups.Count == 0)
        {
            content.Append("<p>Non esistono ancora backup automatici per questa campagna.</p></main>");
            return Page("Mondo non leggibile", content.ToString());
        }

        content.Append("<section class=\"recovery-panel\"><h2>Ripristina un backup</h2><p>La versione attuale verrà conservata prima del recupero.</p><div class=\"recovery-list\">");
        foreach (var backup in backups.Take(10))
        {
            content.Append("<form method=\"post\" action=\"/campaign/restore-backup\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"backup\" value=\"{Encode(backup.FileName)}\">");
            content.Append($"<span>{Encode(backup.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy · HH:mm:ss"))}</span><button type=\"submit\" class=\"secondary\">Ripristina</button></form>");
        }
        content.Append("</div></section></main>");
        return Page("Recupera campagna", content.ToString());
    }

    private static string CampaignControls(
        string worldFile,
        string actionToken,
        IReadOnlyList<CampaignEntry> campaigns)
    {
        var activeFileName = Path.GetFileName(worldFile);
        var content = new StringBuilder();
        content.Append("<section class=\"campaign-control\"><div><p class=\"eyebrow\">Campagne</p><h2>Gestisci il mondo</h2><p>Cambia salvataggio o crea una campagna pulita dallo stesso modello.</p></div><div class=\"campaign-forms\"><form method=\"post\" action=\"/campaign/select\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"campaign\">Campagna attiva</label><select id=\"campaign\" name=\"campaign\">");
        foreach (var campaign in campaigns)
        {
            var selected = campaign.FileName.Equals(
                activeFileName,
                StringComparison.OrdinalIgnoreCase)
                ? " selected"
                : string.Empty;
            content.Append($"<option value=\"{Encode(campaign.FileName)}\"{selected}>{Encode(Path.GetFileNameWithoutExtension(campaign.FileName))} · {campaign.EventCount} eventi</option>");
        }

        content.Append("</select><button type=\"submit\">Apri campagna</button></form><form method=\"post\" action=\"/campaign/create\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"campaign-name\">Nuova campagna</label><input id=\"campaign-name\" name=\"name\" maxlength=\"80\" placeholder=\"Le cronache del villaggio\" required><button type=\"submit\" class=\"secondary\">Crea</button></form></div></section>");
        return content.ToString();
    }

    private static string Metric(string label, string value) =>
        $"<article><span>{Encode(label)}</span><strong>{Encode(value)}</strong></article>";
    private static string WeatherLabel(WeatherCondition condition) =>
        condition switch
        {
            WeatherCondition.Clear => "Sereno",
            WeatherCondition.Cloudy => "Nuvoloso",
            WeatherCondition.Rain => "Pioggia",
            WeatherCondition.Storm => "Tempesta",
            _ => condition.ToString()
        };
    private static string PlayerLiveUpdate(
        EntityId entityId,
        string viewVersion)
    {
        var endpoint = "/player/" +
            Uri.EscapeDataString(entityId.ToString()) +
            "/version";
        return "<aside id=\"player-update\" class=\"player-update\" hidden><p><strong>Il mondo è cambiato.</strong> Hai del testo non inviato, quindi la pagina non è stata aggiornata.</p><button id=\"apply-player-update\" type=\"button\">Mostra le novità</button></aside>" +
            "<script>(()=>{const endpoint='" + endpoint +
            "';const loaded='" + Encode(viewVersion) +
            "';const draft=document.getElementById('proposed-action');const notice=document.getElementById('player-update');const apply=document.getElementById('apply-player-update');let checking=false;async function check(){if(checking||document.hidden)return;checking=true;try{const response=await fetch(endpoint,{cache:'no-store',headers:{Accept:'application/json'}});if(!response.ok)return;const state=await response.json();if(state.version===loaded)return;if(draft&&(!draft.value.trim()&&document.activeElement!==draft)){location.reload();return;}notice.hidden=false;}catch{}finally{checking=false;}}apply.addEventListener('click',()=>location.reload());window.setInterval(check,5000);document.addEventListener('visibilitychange',()=>{if(!document.hidden)check();});})();</script>";
    }
    private static string ContentVersion(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    private static string Stat(string label, string value) =>
        $"<div><span>{Encode(label)}</span><strong>{Encode(value)}</strong></div>";
    private static string Page(string title, string content) =>
        "<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{Encode(title)} · TessitoreGM</title><link rel=\"stylesheet\" href=\"/styles.css\"></head><body><div class=\"shell\">{content}</div></body></html>";
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
