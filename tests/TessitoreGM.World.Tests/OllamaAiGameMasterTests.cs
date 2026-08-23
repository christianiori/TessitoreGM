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
              "playerActionId": "{{context.PlayerActionId}}",
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
