namespace GifJam.Api.Features.AiPhrases;

public sealed record AiPhraseSlot(
    string Id,
    IReadOnlyList<string> RequiredPlayerNames);

public sealed record AiPhraseGenerationRequest(
    int RoundNumber,
    IReadOnlyList<string> PlayerNames,
    IReadOnlyList<AiPhraseSlot> Slots);

public sealed record GeneratedAiPhrase(string SlotId, string Text);

public interface IAiPhraseProvider
{
    Task<IReadOnlyList<GeneratedAiPhrase>> GenerateAsync(
        AiPhraseGenerationRequest request,
        CancellationToken cancellationToken);
}
