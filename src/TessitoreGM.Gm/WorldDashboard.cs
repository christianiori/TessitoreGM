using System.Net;
using System.Text;
using TessitoreGM.Core;
using TessitoreGM.Events;
using TessitoreGM.Narration;
using TessitoreGM.World;

namespace TessitoreGM.Gm;

internal static class WorldDashboard
{
    public static string ResolveWorldFile(string[] args)
    {
        var suppliedPath = args.FirstOrDefault(argument =>
            !argument.StartsWith("--", StringComparison.Ordinal));
        return Path.GetFullPath(suppliedPath ?? "village.json");
    }

    public static string Render(string worldFile)
    {
        try
        {
            return File.Exists(worldFile)
                ? RenderWorld(worldFile)
                : RenderMissingWorld(worldFile);
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

    private static string RenderWorld(string worldFile)
    {
        var eventLog = new WorldEventJsonSerializer().Deserialize(
            File.ReadAllText(worldFile));
        var balances = eventLog.InitialWorld.Balances.ToDictionary(
            balance => balance.EntityId,
            balance => balance.Amount);
        var initialWorld = WorldSnapshot.Create(
            eventLog.InitialWorld.CurrentTime,
            balances,
            eventLog.InitialWorld.ResourceStocks ??
                Array.Empty<EntityResourceStock>());
        var world = new WorldEventProcessor().Replay(initialWorld, eventLog.Events);
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

        content.Append("<header class=\"hero\"><div><p class=\"eyebrow\">Tavolo del GM</p><h1>Il mondo, adesso</h1><p class=\"lede\">Una vista leggibile dello stato persistente della campagna.</p></div><button type=\"button\" onclick=\"location.reload()\">Aggiorna</button></header>");
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
        return facts;
    }

    private static string RenderMissingWorld(string worldFile) => Page(
        "Apri un mondo",
        "<main class=\"empty-state\"><p class=\"eyebrow\">TessitoreGM</p><h1>Il tavolo è pronto.</h1><p>Crea prima un villaggio persistente, poi ricarica questa pagina.</p><pre>dotnet run --project src/TessitoreGM.Console -- create-village village.json</pre>" +
        $"<p class=\"path\">File atteso: <code>{Encode(worldFile)}</code></p><button type=\"button\" onclick=\"location.reload()\">Riprova</button></main>");

    private static string Metric(string label, string value) =>
        $"<article><span>{Encode(label)}</span><strong>{Encode(value)}</strong></article>";
    private static string Stat(string label, string value) =>
        $"<div><span>{Encode(label)}</span><strong>{Encode(value)}</strong></div>";
    private static string Page(string title, string content) =>
        "<!doctype html><html lang=\"it\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{Encode(title)} · TessitoreGM</title><link rel=\"stylesheet\" href=\"/styles.css\"></head><body><div class=\"shell\">{content}</div></body></html>";
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
