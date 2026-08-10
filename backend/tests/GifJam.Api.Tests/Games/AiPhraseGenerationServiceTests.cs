using GifJam.Api.Common.Random;
using GifJam.Api.Features.AiPhrases;
using Microsoft.Extensions.Logging.Abstractions;

namespace GifJam.Api.Tests.Games;

public sealed class AiPhraseGenerationServiceTests
{
    [Fact]
    public async Task FourPlayersCreateFourPhrasesWithTwoRequiredPairs()
    {
        var provider = new RecordingProvider();
        var service = new AiPhraseGenerationService(
            provider,
            new PredictableRandomizer(),
            NullLogger<AiPhraseGenerationService>.Instance);

        var phrases = await service.GenerateAsync(
            ["Mitch", "Ferreiro", "Bia", "Eduardo"],
            1,
            CancellationToken.None);

        Assert.Equal(4, phrases.Count);
        var request = Assert.IsType<AiPhraseGenerationRequest>(provider.LastRequest);
        var pairedSlots = request.Slots.Where(slot => slot.RequiredPlayerNames.Count == 2).ToArray();
        Assert.Equal(2, pairedSlots.Length);
        Assert.Equal(4, pairedSlots.SelectMany(slot => slot.RequiredPlayerNames).Distinct().Count());
        Assert.All(pairedSlots, slot => Assert.Contains(
            phrases,
            phrase => slot.RequiredPlayerNames.All(name =>
                phrase.Contains(name, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public async Task InvalidProviderPayloadUsesACompleteLocalFallback()
    {
        var service = new AiPhraseGenerationService(
            new InvalidProvider(),
            new PredictableRandomizer(),
            NullLogger<AiPhraseGenerationService>.Instance);

        var phrases = await service.GenerateAsync(
            ["Mitch", "Ferreiro", "Bia", "Eduardo", "Luna"],
            2,
            CancellationToken.None);

        Assert.Equal(5, phrases.Count);
        Assert.Equal(5, phrases.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(phrases, phrase => phrase.Contains("Mitch", StringComparison.OrdinalIgnoreCase) &&
            phrase.Contains("Ferreiro", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(phrases, phrase => phrase.Contains("Bia", StringComparison.OrdinalIgnoreCase) &&
            phrase.Contains("Eduardo", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingProvider : IAiPhraseProvider
    {
        public AiPhraseGenerationRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<GeneratedAiPhrase>> GenerateAsync(
            AiPhraseGenerationRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            IReadOnlyList<GeneratedAiPhrase> phrases = request.Slots
                .Select(slot => new GeneratedAiPhrase(
                    slot.Id,
                    slot.RequiredPlayerNames.Count == 2
                        ? $"Quando {slot.RequiredPlayerNames[0]} encontra {slot.RequiredPlayerNames[1]} na rua."
                        : $"Uma situacao geral e inesperada em {slot.Id}."))
                .ToArray();
            return Task.FromResult(phrases);
        }
    }

    private sealed class InvalidProvider : IAiPhraseProvider
    {
        public Task<IReadOnlyList<GeneratedAiPhrase>> GenerateAsync(
            AiPhraseGenerationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GeneratedAiPhrase>>([]);
    }

    private sealed class PredictableRandomizer : IRandomizer
    {
        public int NextInt32(int exclusiveUpperBound) => 0;

        public void Shuffle<T>(IList<T> items)
        {
        }
    }
}
