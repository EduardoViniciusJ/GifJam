using GifJam.Api.Common.Errors;
using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Games.Services;

public static class GameSettingsValidator
{
    public static void Validate(
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds,
        GameMode mode)
    {
        if (totalRounds is < 3 or > 6)
        {
            throw new BadRequestException(
                "invalid_round_count",
                "Total rounds must be between 3 and 6.");
        }

        if (phraseSubmissionSeconds is not (30 or 60 or 90))
        {
            throw new BadRequestException(
                "invalid_phrase_duration",
                "Phrase duration must be 30, 60 or 90 seconds.");
        }

        if (resultsSeconds is not (15 or 30 or 60))
        {
            throw new BadRequestException(
                "invalid_results_duration",
                "Results duration must be 15, 30 or 60 seconds.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new BadRequestException(
                "invalid_game_mode",
                "The selected game mode is invalid.");
        }
    }
}
