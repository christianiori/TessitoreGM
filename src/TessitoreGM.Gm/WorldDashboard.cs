using System.Net;
using System.Text;
using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;
using TessitoreGM.Npcs;
using TessitoreGM.World;

namespace TessitoreGM.Gm;

internal static class WorldDashboard
{
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
        IReadOnlyList<CampaignEntry> campaigns)
    {
        try
        {
            return File.Exists(worldFile)
                ? RenderWorld(worldFile, actionToken, campaigns)
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

    public static void Advance(string worldFile, TimeSpan duration)
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
        var updatedLog = eventLog with
        {
            Events = eventLog.Events.Concat(result.ProducedEvents).ToArray()
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
        var npc = simulation.Npcs.FirstOrDefault(candidate =>
            candidate.EntityId.ToString() == entityValue)
            ?? throw new ArgumentException("Personaggio non valido.");
        var location = (simulation.Locations ??
            Array.Empty<LocationPresentationDefinition>())
            .FirstOrDefault(candidate =>
                candidate.LocationId.ToString() == locationValue)
            ?? throw new ArgumentException("Luogo non valido.");
        var world = Replay(eventLog);

        if (world.GetLocation(npc.EntityId) == location.LocationId)
        {
            throw new ArgumentException(
                "Il personaggio si trova già nel luogo scelto.");
        }

        var movement = new EntityEnteredLocation(
            npc.EntityId,
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
            candidate.EntityId.ToString() == entityValue)
            ?? throw new ArgumentException("Personaggio non valido.");
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

        if (KnownFacts(npc, eventLog.Events).Contains(factId))
        {
            throw new ArgumentException(
                "Il personaggio conosce già questa informazione.");
        }

        var world = Replay(eventLog);
        var revealed = new FactRevealed(
            npc.EntityId,
            factId,
            world.CurrentTime);
        var updatedLog = new WorldEventLogEditor().Append(
            eventLog,
            world,
            revealed);

        Save(worldFile, serializer, updatedLog);
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
        IReadOnlyList<CampaignEntry> campaigns)
    {
        var eventLog = new WorldEventJsonSerializer().Deserialize(
            File.ReadAllText(worldFile));
        var world = Replay(eventLog);
        var simulation = eventLog.Simulation;
        var entityNames = simulation?.Entities?.ToDictionary(
            entity => entity.EntityId,
            entity => entity.Name) ?? new Dictionary<EntityId, string>();
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

        content.Append("<header class=\"hero\"><div><p class=\"eyebrow\">Tavolo del GM</p><h1>Il mondo, adesso</h1><p class=\"lede\">Una vista leggibile dello stato persistente della campagna.</p></div><button type=\"button\" class=\"secondary\" onclick=\"location.reload()\">Aggiorna</button></header>");
        content.Append(CampaignControls(
            worldFile,
            actionToken,
            campaigns));
        content.Append("<section class=\"time-control\"><div><p class=\"eyebrow\">Simulazione</p><h2>Avanza il tempo</h2><p>Lascia che il villaggio agisca autonomamente fino al nuovo orario.</p></div><form method=\"post\" action=\"/advance\">");
        content.Append($"<input type=\"hidden\" name=\"token\" value=\"{Encode(actionToken)}\">");
        content.Append("<label for=\"hours\">Intervallo</label><select id=\"hours\" name=\"hours\"><option value=\"1\">1 ora</option><option value=\"6\">6 ore</option><option value=\"24\">1 giorno</option></select><button type=\"submit\">Avanza</button></form></section>");
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

        content.Append("</select><label for=\"fact\">Informazione</label><select id=\"fact\" name=\"fact\">");
        foreach (var factId in availableFacts)
        {
            content.Append($"<option value=\"{Encode(factId.ToString())}\">{Encode(factId.ToString())}</option>");
        }

        content.Append("</select><label for=\"new-fact\">Oppure nuovo ID</label><input id=\"new-fact\" name=\"newFact\" maxlength=\"100\" placeholder=\"village:bandits-seen\"><button type=\"submit\">Rivela informazione</button></form></section>");
        content.Append("<main><section class=\"world-strip\">");
        content.Append(Metric("Ora del mondo", world.CurrentTime.ToString("dd MMM yyyy · HH:mm")));
        content.Append(Metric("Eventi registrati", eventLog.Events.Count.ToString()));
        content.Append(Metric("Personaggi attivi", (simulation?.Npcs.Count ?? 0).ToString()));
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

        content.Append("</div></section><section class=\"chronicle\"><div class=\"section-heading\"><p class=\"eyebrow\">Registro</p><h2>Ultimi avvenimenti</h2></div><ol>");
        foreach (var line in narration.TakeLast(12).Reverse())
        {
            content.Append($"<li><time>{Encode(line.OccurredAt.ToString("dd/MM · HH:mm"))}</time><p>{Encode(line.Text)}</p></li>");
        }

        content.Append("</ol></section></main><footer><span>Salvataggio</span>");
        content.Append($"<code>{Encode(worldFile)}</code></footer>");
        return Page("Tavolo del GM", content.ToString());
    }

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
