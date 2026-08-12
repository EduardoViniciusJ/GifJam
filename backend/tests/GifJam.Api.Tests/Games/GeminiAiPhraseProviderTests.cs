using System.Net;
using System.Text;
using System.Text.Json;
using GifJam.Api.Features.AiPhrases;
using GifJam.Api.Integrations.Gemini;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Games;

public sealed class GeminiAiPhraseProviderTests
{
    [Fact]
    public async Task SendsServerSideKeyAndParsesStructuredPhraseResponse()
    {
        var handler = new StubHandler();
        using var client = new HttpClient(handler)
        {
            BaseAddress = new("https://generativelanguage.googleapis.test/v1beta/")
        };
        var provider = new GeminiAiPhraseProvider(client, Options.Create(new GeminiOptions
        {
            ApiKey = "server-side-test-key",
            Model = "gemini-test-model",
            BaseUrl = client.BaseAddress.ToString()
        }));
        var request = new AiPhraseGenerationRequest(
            1,
            ["Mitch", "Ferreiro"],
            [new("pair-1", ["Mitch", "Ferreiro"]), new("general-1", [])]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("server-side-test-key", handler.ApiKey);
        Assert.Equal(
            "https://generativelanguage.googleapis.test/v1beta/models/gemini-test-model:generateContent",
            handler.RequestUrl);
        Assert.DoesNotContain("server-side-test-key", handler.RequestUrl, StringComparison.Ordinal);
        Assert.Contains("responseJsonSchema", handler.RequestBody, StringComparison.Ordinal);
        using var requestPayload = JsonDocument.Parse(handler.RequestBody);
        var systemPrompt = requestPayload.RootElement
            .GetProperty("systemInstruction")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        var userPrompt = requestPayload.RootElement
            .GetProperty("contents")[0]
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        Assert.Contains("situação claramente fictícia", systemPrompt, StringComparison.Ordinal);
        Assert.Contains("contexto não óbvio", userPrompt, StringComparison.Ordinal);
        Assert.Equal(["pair-1", "general-1"], result.Select(phrase => phrase.SlotId));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public string ApiKey { get; private set; } = string.Empty;

        public string RequestUrl { get; private set; } = string.Empty;

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ApiKey = Assert.Single(request.Headers.GetValues("x-goog-api-key"));
            RequestUrl = request.RequestUri?.ToString() ?? string.Empty;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (CallCount == 1)
            {
                return new(HttpStatusCode.TooManyRequests);
            }

            var phraseJson = JsonSerializer.Serialize(new
            {
                phrases = new[]
                {
                    new { slotId = "pair-1", text = "Quando Mitch encontra Ferreiro na rua." },
                    new { slotId = "general-1", text = "Quando o plano perfeito dura cinco segundos." }
                }
            });
            var responseJson = JsonSerializer.Serialize(new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[] { new { text = $"Here is the JSON:\n```json\n{phraseJson}\n```" } }
                        }
                    }
                }
            });
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
