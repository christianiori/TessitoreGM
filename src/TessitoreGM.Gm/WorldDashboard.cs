using System.Net;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;
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
        "<form method=\"post\" action=\"/login\"><label for=\"access-code\">Codice di accesso</label><input id=\"access-code\" name=\"accessCode\" inputmode=\"numeric\" autocomplete=\"one-time-code\" pattern=\"[0-9]{8}\" maxlength=\"8\" required autofocus><button type=\"submit\">Accedi</button></form></section></main>");

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
        PendingWorldAdvance? pendingAdvance)
    {
        try
        {
            return File.Exists(worldFile)
                ? RenderWorld(
                    worldFile,
                    actionToken,
                    campaigns,
                    pendingAdvance)
                : RenderMissingWorld(
                    worldFile,
                    actionToken,
                    campaigns);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or
            InvalidOperationException or ArgumentException)
        {
            return Page(
                "Mondo non leggibile",
                $"<main class=\"empty-state\"><p class=\"eyebrow\">" +
                "TessitoreGM</p><h1>Non riesco ad aprire questo mondo.</h1>" +
                $"<p>{Encode(exception.Message)}</p>" +
                $"<code>{Encode(worldFile)}</code></main>");
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

            content.Append("<header class=\"hero chronicle-hero\"><div><p class=\"eyebrow\">Cronaca della campagna</p><h1>CiÃ² che Ã¨ accaduto</h1><p class=\"lede\">Il racconto completo e deterministico ricostruito dal registro del mondo.</p></div><div class=\"hero-actions\"><a class=\"button secondary\" href=\"/\">Torna al tavolo</a><button type=\"button\" onclick=\"window.print()\">Stampa</button></div></header>");
            content.Append("<main><section class=\"world-strip chronicle-summary\">");
            content.Append(Metric(
                "Intervallo",
                $"{firstTime:dd MMM yyyy Â· HH:mm} â€“ {lastTime:dd MMM yyyy Â· HH:mm}"));
            content.Append(Metric("Avvenimenti", narration.Count.ToString()));
            content.Append(Metric("Azioni dei giocatori", playerActions.ToString()));
            content.Append("</section><section class=\"chronicle chronicle-full\"><div class=\"section-heading\"><p class=\"eyebrow\">Registro completo</p><h2>Tutti gli avvenimenti</h2></div><ol>");
            foreach (var line in narration)
            {
                content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM/yyyy Â· HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
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

    public static string RenderPlayer(
        string worldFile,
        string entityValue,
        string playerInteractionToken)
    {
        try
        {
            var eventLog = new WorldEventJsonSerializer().Deserialize(
                File.ReadAllText(worldFile));
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
            var content = new StringBuilder();

            content.Append("<header class=\"player-hero\"><div><p class=\"eyebrow\">Tavolo del giocatore</p>");
            content.Append($"<h1>{Encode(character.Name)}</h1><p class=\"lede\">Ciò che il tuo personaggio può osservare e ricordare, adesso.</p></div><button type=\"button\" class=\"secondary\" onclick=\"location.reload()\">Aggiorna</button></header>");
            content.Append("<main class=\"player-view\"><section class=\"world-strip player-summary\">");
            content.Append(Metric("Luogo", locationName));
            content.Append(Metric(
                "Monete",
                world.GetBalance(character.EntityId).ToString()));
            content.Append(Metric("Ora del mondo", world.CurrentTime.ToString(
                "dd MMM yyyy · HH:mm")));
            content.Append("</section><section class=\"player-action-box\"><div class=\"section-heading\"><p class=\"eyebrow\">La tua azione</p><h2>Cosa vuoi fare?</h2></div><form method=\"post\" action=\"/player/");
            content.Append(Uri.EscapeDataString(character.EntityId.ToString()));
            content.Append("/actions\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(playerInteractionToken)}\"><label for=\"proposed-action\">Descrivi l'intenzione del personaggio</label><textarea id=\"proposed-action\" name=\"description\" maxlength=\"500\" rows=\"4\" placeholder=\"Cerco tracce vicino al granaio.\" required></textarea><button type=\"submit\">Invia al GM</button></form></section>");
            content.Append("<section><div class=\"section-heading\"><p class=\"eyebrow\">Richieste</p><h2>Le tue azioni</h2></div><div class=\"player-action-list\">");
            if (ownActions.Length == 0)
            {
                content.Append("<p class=\"empty-copy\">Non hai ancora proposto azioni.</p>");
            }
            foreach (var action in ownActions)
            {
                content.Append("<article class=\"player-action-item\">");
                content.Append($"<div><span class=\"action-status\">{Encode(PlayerActionStatusLabel(action.Status))}</span><p>{Encode(action.Description)}</p></div>");
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
                "La quantitÃ  di monete deve essere maggiore di zero.");
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
                "La quantitÃ  deve essere maggiore di zero.");
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
                "Esiste giÃ  un personaggio giocante con questo nome.");
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

    public static void SubmitPlayerAction(
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
                "L'azione non Ã¨ piÃ¹ in attesa di una decisione.");
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
                "L'azione non puÃ² ricevere un nuovo tiro.");
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

    public static void RollD20(
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
                "Questo tiro non Ã¨ disponibile.");
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
    }

    private static void Save(
        string worldFile,
        WorldEventJsonSerializer serializer,
        WorldEventLog eventLog)
    {
        var temporaryFile = worldFile + ".tmp";

        File.WriteAllText(temporaryFile, serializer.Serialize(eventLog));
        File.Move(temporaryFile, worldFile, overwrite: true);
    }

    private static string RenderWorld(
        string worldFile,
        string actionToken,
        IReadOnlyList<CampaignEntry> campaigns,
        PendingWorldAdvance? pendingAdvance)
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
        var narration = new DeterministicNarrator().Narrate(
            eventLog.Events,
            new NarrationContext(entityNames, locationNames, resourceNames, needNames));
        var content = new StringBuilder();

        content.Append("<header class=\"hero\"><div><p class=\"eyebrow\">Tavolo del GM</p><h1>Il mondo, adesso</h1><p class=\"lede\">Una vista leggibile dello stato persistente della campagna.</p></div><div class=\"hero-actions\"><a class=\"button secondary\" href=\"/chronicle\">Cronaca completa</a><button type=\"button\" class=\"secondary\" onclick=\"location.reload()\">Aggiorna</button></div></header>");
        content.Append(CampaignControls(
            worldFile,
            actionToken,
            campaigns));
        content.Append(RenderPlayerActionQueue(
            eventLog,
            playerCharacters,
            actionToken));
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Compagnia</p><h2>Personaggi giocanti</h2><p>Aggiungi un personaggio controllato dai giocatori. Il motore non prenderÃ  decisioni al suo posto.</p></div><form method=\"post\" action=\"/player-character\">");
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
        var availableFacts = simulation is null
            ? Array.Empty<FactId>()
            : AvailableFacts(simulation, eventLog.Events);
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
        foreach (var entity in CharacterOptions(simulation, playerCharacters, entityNames))
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"coin-payee\">A</label><select id=\"coin-payee\" name=\"payee\">");
        foreach (var entity in CharacterOptions(simulation, playerCharacters, entityNames))
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"coin-amount\">Monete</label><input id=\"coin-amount\" name=\"amount\" type=\"number\" min=\"1\" max=\"1000000\" value=\"1\" required><label for=\"coin-reason\">Motivazione</label><input id=\"coin-reason\" name=\"reason\" maxlength=\"200\" placeholder=\"Ricompensa per l'incarico\" required><button type=\"submit\">Registra trasferimento</button></form></section>");
        content.Append("<section class=\"world-action\"><div><p class=\"eyebrow\">Conseguenze</p><h2>Gestisci una risorsa</h2><p>Registra un'acquisizione, una perdita o un trasferimento motivato.</p></div><form method=\"post\" action=\"/resources/change\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"resource-operation\">Operazione</label><select id=\"resource-operation\" name=\"operation\"><option value=\"acquire\">Acquisisce</option><option value=\"lose\">Perde</option><option value=\"transfer\">Trasferisce</option></select><label for=\"resource-entity\">Personaggio</label><select id=\"resource-entity\" name=\"entity\">");
        foreach (var entity in CharacterOptions(simulation, playerCharacters, entityNames))
        {
            content.Append($"<option value=\"{Encode(entity.Id.ToString())}\">{Encode(entity.Name)}</option>");
        }
        content.Append("</select><label for=\"resource-destination\">Destinatario</label><select id=\"resource-destination\" name=\"destination\">");
        foreach (var entity in CharacterOptions(simulation, playerCharacters, entityNames))
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
        content.Append("</select><label for=\"player-description\">Azione</label><textarea id=\"player-description\" name=\"description\" maxlength=\"500\" rows=\"3\" placeholder=\"Convince la guardia ad aprire il cancello.\" required></textarea><button type=\"submit\">Registra nella cronaca</button></form></section>");
        content.Append("<main><section class=\"world-strip\">");
        content.Append(Metric("Ora del mondo", world.CurrentTime.ToString("dd MMM yyyy · HH:mm")));
        content.Append(Metric("Eventi registrati", eventLog.Events.Count.ToString()));
        content.Append(Metric("Personaggi attivi", ((simulation?.Npcs.Count ?? 0) + playerCharacters.Count).ToString()));
        content.Append("</section><section><div class=\"section-heading\"><p class=\"eyebrow\">Presenze</p><h2>Personaggi</h2></div><div class=\"npc-grid\">");

        foreach (var npc in simulation?.Npcs ?? Array.Empty<NpcSimulationDefinition>())
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

        foreach (var character in playerCharacters)
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
            content.Append($"<a class=\"player-view-link\" href=\"/player/{Uri.EscapeDataString(character.EntityId.ToString())}\">Apri vista giocatore</a></article>");
        }

        content.Append("</div></section><section class=\"chronicle\"><div class=\"section-heading\"><p class=\"eyebrow\">Registro</p><h2>Ultimi avvenimenti</h2></div><ol>");
        foreach (var line in narration.TakeLast(12).Reverse())
        {
            content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM · HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
        }

        content.Append("</ol></section></main><footer><span>Salvataggio</span>");
        content.Append($"<code>{Encode(worldFile)}</code></footer>");
        return Page("Tavolo del GM", content.ToString());
    }

    private static string RenderPlayerActionQueue(
        WorldEventLog eventLog,
        IReadOnlyList<PlayerCharacterRegistered> playerCharacters,
        string actionToken)
    {
        var activeActions = (eventLog.PlayerActions ??
                Array.Empty<PlayerActionProposal>())
            .Where(action => action.Status is
                PlayerActionStatus.Pending or
                PlayerActionStatus.RollRequested or
                PlayerActionStatus.Rolled)
            .OrderBy(action => action.SubmittedAt)
            .ToArray();
        var names = playerCharacters.ToDictionary(
            character => character.EntityId,
            character => character.Name);
        var content = new StringBuilder();
        content.Append("<section class=\"gm-action-queue\"><div class=\"section-heading\"><p class=\"eyebrow\">Giocatori</p><h2>Azioni in attesa</h2></div>");
        if (activeActions.Length == 0)
        {
            content.Append("<div class=\"player-panel\"><p>Nessuna azione attende una decisione.</p></div></section>");
            return content.ToString();
        }
        content.Append("<div class=\"gm-action-list\">");
        foreach (var action in activeActions)
        {
            var playerName = names.GetValueOrDefault(
                action.PlayerCharacterId,
                action.PlayerCharacterId.ToString());
            content.Append("<article class=\"gm-action-item\"><div class=\"gm-action-copy\">");
            content.Append($"<span class=\"action-status\">{Encode(playerName)} · {Encode(PlayerActionStatusLabel(action.Status))}</span><p>{Encode(action.Description)}</p>");
            if (action.Roll is not null)
            {
                content.Append($"<div class=\"roll-summary\">{Encode(RollSummary(action.Roll, includeSecretDifficulty: true))}</div>");
            }
            content.Append("</div>");
            if (action.Status == PlayerActionStatus.Pending)
            {
                content.Append("<form class=\"roll-request-form\" method=\"post\" action=\"/player-actions/request-roll\">");
                content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><label>ModalitÃ <select name=\"mode\"><option value=\"Normal\">Normale</option><option value=\"Advantage\">Vantaggio</option><option value=\"Disadvantage\">Svantaggio</option></select></label><label>Modificatore<input name=\"modifier\" type=\"number\" min=\"-20\" max=\"20\" value=\"0\" required></label><label>DifficoltÃ <input name=\"difficulty\" type=\"number\" min=\"1\" max=\"40\" placeholder=\"opzionale\"></label><label class=\"checkbox-label\"><input name=\"difficultyVisible\" type=\"checkbox\"> Mostra la difficoltÃ </label><button type=\"submit\" class=\"secondary\">Richiedi d20</button></form>");
            }
            content.Append("<form class=\"action-resolution-form\" method=\"post\" action=\"/player-actions/resolve\">");
            content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\"><input type=\"hidden\" name=\"actionId\" value=\"{action.Id}\"><label>Esito<textarea name=\"resolution\" maxlength=\"500\" rows=\"3\" placeholder=\"Descrivi ciÃ² che accade.\" required></textarea></label><div class=\"resolution-buttons\"><button name=\"decision\" value=\"approve\" type=\"submit\">Approva</button><button name=\"decision\" value=\"reject\" type=\"submit\" class=\"secondary\">Rifiuta</button></div></form></article>");
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
                ? $", difficoltÃ  {roll.Difficulty}"
                : ", difficoltÃ  segreta";
        if (roll.Dice is null || roll.Total is null)
        {
            return $"d20: {mode}, modificatore {roll.Modifier:+#;-#;0}{difficulty}.";
        }
        var dice = string.Join(" e ", roll.Dice);
        var natural = roll.KeptDie is 1 or 20
            ? $" · {roll.KeptDie} naturale"
            : string.Empty;
        return $"Dadi: {dice}; risultato {roll.KeptDie} " +
            $"{roll.Modifier:+#;-#;+0} = {roll.Total}{natural}{difficulty}.";
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
                Array.Empty<EntityResourceStock>());
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
    private static string Stat(string label, string value) =>
        $"<div><span>{Encode(label)}</span><strong>{Encode(value)}</strong></div>";
    private static string Page(string title, string content) =>
        "<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{Encode(title)} · TessitoreGM</title><link rel=\"stylesheet\" href=\"/styles.css\"></head><body><div class=\"shell\">{content}</div></body></html>";
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
