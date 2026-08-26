using System.Net;
using System.Text;
using System.Text.Json;
using TessitoreGM.AiGm;
using TessitoreGM.Core;

namespace TessitoreGM.World.Tests;

public sealed class OllamaAiGameMasterTests
{
    [Fact]
    public async Task PlanTurnAsync_SendsStructuredLocalRequestAndReadsPlan()
    {
        var context = CreateContext();
        string? requestBody = null;
        Uri? requestUri = null;
        using var client = new HttpClient(new StubHandler(async request =>
        {
            requestUri = request.RequestUri;
            requestBody = await request.Content!.ReadAsStringAsync();
            var plan = $$"""
            {
              "narration": "L'oste ascolta e indica la porta.",
              "roll": null,
              "consequences": []
            }
            """;
            var response = JsonSerializer.Serialize(new
            {
                message = new { role = "assistant", content = plan }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response,
                    Encoding.UTF8,
                    "application/json")
            };
        }));

        var result = await new OllamaAiGameMaster(
            client,
            "qwen2.5:7b").PlanTurnAsync(context);

        Assert.Equal(context.PlayerActionId, result.PlayerActionId);
        Assert.Equal(
            "http://127.0.0.1:11434/api/chat",
            requestUri?.ToString());
        using var requestJson = JsonDocument.Parse(requestBody!);
        Assert.Equal(
            "qwen2.5:7b",
            requestJson.RootElement.GetProperty("model").GetString());
        Assert.False(requestJson.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(
            "object",
            requestJson.RootElement
                .GetProperty("format")
                .GetProperty("type")
                .GetString());
        Assert.False(
            requestJson.RootElement
                .GetProperty("format")
                .GetProperty("properties")
                .TryGetProperty("playerActionId", out _));
        Assert.Contains(
            context.PlayerActionId.ToString(),
            requestBody);
    }

    [Fact]
    public async Task PlanTurnAsync_UnreachableOllamaUsesSafeProviderFailure()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            throw new HttpRequestException("Connection refused.")));
        var gameMaster = new OllamaAiGameMaster(client, "qwen2.5:7b");

        var exception = await Assert.ThrowsAsync<
            AiGmProviderUnavailableException>(() =>
                gameMaster.PlanTurnAsync(CreateContext()));

        Assert.Contains("non è raggiungibile", exception.Message);
    }

    [Fact]
    public async Task PlanTurnAsync_RetriesOnceWithRelevanceFeedback()
    {
        var npcId = new EntityId("innkeeper");
        var context = CreateContext() with
        {
            ActionFrame = new AiGmActionFrame(
                AiGmActionKind.Social,
                [new AiGmActionTarget(npcId, "Mira l'ostessa", true)],
                new LocationId("inn"),
                "Locanda",
                false,
                false)
        };
        var requestBodies = new List<string>();
        using var client = new HttpClient(new StubHandler(async request =>
        {
            requestBodies.Add(await request.Content!.ReadAsStringAsync());
            var narration = requestBodies.Count == 1
                ? "Alcuni utensili sono accatastati in un angolo."
                : "Mira l'ostessa ride e risponde con una battuta ancora più secca.";
            var plan = JsonSerializer.Serialize(new
            {
                narration,
                roll = (object?)null,
                consequences = Array.Empty<object>()
            });
            var response = JsonSerializer.Serialize(new
            {
                message = new { role = "assistant", content = plan }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response,
                    Encoding.UTF8,
                    "application/json")
            };
        }));

        var result = await new OllamaAiGameMaster(
            client,
            "qwen2.5:7b").PlanTurnAsync(context);

        Assert.Equal(2, requestBodies.Count);
        using var retryRequest = JsonDocument.Parse(requestBodies[1]);
        var retryPrompt = retryRequest.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("CORREZIONE OBBLIGATORIA", retryPrompt);
        Assert.Contains("Mira l'ostessa", retryPrompt);
        Assert.Contains("ride", result.Narration);
    }

    [Fact]
    public async Task PlanTurnAsync_RetriesUnobservableNpcIntent()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            var narration = calls == 1
                ? "L'oste sorride, sperando di convincerti."
                : "L'oste sorride e appoggia entrambe le mani sul bancone.";
            var plan = JsonSerializer.Serialize(new
            {
                narration,
                roll = (object?)null,
                consequences = Array.Empty<object>()
            });
            var response = JsonSerializer.Serialize(new
            {
                message = new { role = "assistant", content = plan }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response,
                    Encoding.UTF8,
                    "application/json")
            });
        }));

        var result = await new OllamaAiGameMaster(
            client,
            "qwen2.5:7b").PlanTurnAsync(CreateContext());

        Assert.Equal(2, calls);
        Assert.DoesNotContain("sperando", result.Narration);
        Assert.Contains("mani", result.Narration);
    }

    [Fact]
    public void Constructor_RejectsRemoteEndpoint()
    {
        using var client = new HttpClient();

        var exception = Assert.Throws<ArgumentException>(() =>
            new OllamaAiGameMaster(
                client,
                "qwen2.5:7b",
                new Uri("https://example.com/")));

        Assert.Contains("local HTTP endpoint", exception.Message);
    }

    private static AiGmTurnContext CreateContext() => new(
        Guid.NewGuid(),
        new EntityId("player"),
        "Ada",
        "Chiedo all'oste che cosa c'è dietro la porta.",
        new AiGmWorldState(
            DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
            WeatherCondition.Clear,
            []),
        new AiGmMemoryDossier([], []),
        new AiGmAuthorizedPerspective(
            DateTimeOffset.Parse("2026-08-23T09:00:00Z"),
            WeatherCondition.Clear,
            new AiGmPerspectivePlayer(
                new EntityId("player"),
                "Ada",
                0,
                []),
            new AiGmPerspectiveScene(
                null,
                null,
                []),
            [],
            [],
            []),
        AiGmInvariants.Rules);

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => sendAsync(request);
    }
}
