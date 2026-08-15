using GifJam.Api.Common.Errors;
using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Realtime;

public static class GameHubInputValidator
{
    public static void ValidateGameCode(string? gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode) || gameCode.Length > 5)
        {
            throw new BadRequestException("invalid_game_code", "The game code is invalid.");
        }
    }

    public static void ValidatePhrase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 180)
        {
            throw new BadRequestException("invalid_phrase", "The phrase must contain between 1 and 180 characters.");
        }
    }

    public static void ValidateSelectionToken(string? selectionToken)
    {
        if (string.IsNullOrWhiteSpace(selectionToken) || selectionToken.Length > 4096)
        {
            throw new BadRequestException("invalid_gif_selection", "The GIF selection token is invalid.");
        }
    }

    public static void ValidateRoomVisibility(RoomVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
        {
            throw new BadRequestException("invalid_room_visibility", "The room visibility is invalid.");
        }
    }
}
