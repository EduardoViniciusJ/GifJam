using GifJam.Api.Common.Random;

namespace GifJam.Api.Features.AiPhrases;

public sealed partial class AiPhraseGenerationService(
    IAiPhraseProvider provider,
    IRandomizer randomizer,
    ILogger<AiPhraseGenerationService> logger)
{
    private static readonly string[] GenericFallbacks =
    [
        "Quando voce finge que entendeu a explicacao e alguem pede um resumo.",
        "Quando o plano parecia perfeito ate chegar a hora de executar.",
        "Quando voce abre a camera frontal sem querer em publico.",
        "Quando o grupo diz que vai sair cedo e o sol comeca a nascer.",
        "Quando a reuniao poderia ter sido uma mensagem de duas linhas.",
        "Quando voce lembra de uma vergonha antiga bem na hora de dormir."
    ];

    private static readonly string[] PairedFallbacks =
    [
        "Quando {0} encontra {1} na rua depois de ignorar tres mensagens.",
        "Quando {0} confia em {1} para escolher o caminho mais rapido.",
        "Quando {0} percebe que {1} contou aquela historia para todo mundo.",
        "Quando {0} e {1} dizem que desta vez o plano vai dar certo.",
        "Quando {0} tenta explicar para {1} que aquilo nao foi de proposito.",
        "Quando {0} ve {1} chegando com mais uma ideia duvidosa."
    ];

    public async Task<IReadOnlyList<string>> GenerateAsync(
        IReadOnlyList<string> playerNames,
        int roundNumber,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerNames.Count, 2);

        var normalizedNames = playerNames
            .Select((name, index) => NormalizePlayerName(name, index))
            .ToArray();
        var slots = CreateSlots(normalizedNames);
        var request = new AiPhraseGenerationRequest(roundNumber, normalizedNames, slots);

        try
        {
            var generated = await provider.GenerateAsync(request, cancellationToken);
            return ValidateAndOrder(generated, slots);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogProviderFallback(logger, exception, roundNumber);
            return CreateFallbacks(slots, roundNumber);
        }
    }

    private AiPhraseSlot[] CreateSlots(string[] playerNames)
    {
        var shuffledNames = playerNames.ToList();
        randomizer.Shuffle(shuffledNames);
        var pairedPhraseCount = playerNames.Length / 2;
        var slots = new List<AiPhraseSlot>(playerNames.Length);

        for (var index = 0; index < pairedPhraseCount; index++)
        {
            slots.Add(new(
                $"pair-{index + 1}",
                [shuffledNames[index * 2], shuffledNames[(index * 2) + 1]]));
        }

        for (var index = pairedPhraseCount; index < playerNames.Length; index++)
        {
            slots.Add(new($"general-{index - pairedPhraseCount + 1}", []));
        }

        return [.. slots];
    }

    private static string[] ValidateAndOrder(
        IReadOnlyList<GeneratedAiPhrase> generated,
        AiPhraseSlot[] slots)
    {
        if (generated.Count != slots.Length)
        {
            throw new InvalidOperationException("The AI provider returned an unexpected phrase count.");
        }

        var bySlot = generated
            .GroupBy(phrase => phrase.SlotId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var ordered = new string[slots.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (!bySlot.TryGetValue(slot.Id, out var candidates) || candidates.Length != 1)
            {
                throw new InvalidOperationException("The AI provider returned missing or duplicate phrase slots.");
            }

            var text = candidates[0].Text.Trim();
            if (text.Length is < 1 or > 180)
            {
                throw new InvalidOperationException("The AI provider returned a phrase with an invalid length.");
            }

            if (slot.RequiredPlayerNames.Any(name =>
                    !text.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The AI provider omitted a required player name.");
            }

            ordered[index] = text;
        }

        if (ordered.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
        {
            throw new InvalidOperationException("The AI provider returned duplicate phrases.");
        }

        return ordered;
    }

    private static string[] CreateFallbacks(AiPhraseSlot[] slots, int roundNumber)
    {
        var results = new string[slots.Length];
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            if (slot.RequiredPlayerNames.Count == 2)
            {
                var template = PairedFallbacks[(roundNumber + index) % PairedFallbacks.Length];
                results[index] = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    template,
                    slot.RequiredPlayerNames[0],
                    slot.RequiredPlayerNames[1]);
            }
            else
            {
                results[index] = GenericFallbacks[(roundNumber + index) % GenericFallbacks.Length];
            }
        }

        return results;
    }

    private static string NormalizePlayerName(string name, int index)
    {
        var normalized = name.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? $"Jogador {index + 1}" : normalized;
    }

    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Warning,
        Message = "AI phrase generation failed for round {RoundNumber}; local fallback phrases were used")]
    private static partial void LogProviderFallback(
        ILogger logger,
        Exception exception,
        int roundNumber);
}
