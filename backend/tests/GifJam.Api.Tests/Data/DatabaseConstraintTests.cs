using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Tests.Data;

[Collection(PostgresTestGroup.Name)]
public sealed class DatabaseConstraintTests(PostgresFixture database)
{
    [Fact]
    public async Task DuplicatePhraseForPlayerAndRoundIsRejected()
    {
        var seed = await CreateRoundAsync();
        await using var context = database.CreateDbContext();
        context.Phrases.AddRange(
            CreatePhrase(seed.RoundId, seed.FirstUserId, "First phrase"),
            CreatePhrase(seed.RoundId, seed.FirstUserId, "Second phrase"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicatePhraseVoteForPlayerAndRoundIsRejected()
    {
        var seed = await CreateRoundAsync();
        await using (var setupContext = database.CreateDbContext())
        {
            setupContext.Phrases.Add(CreatePhrase(seed.RoundId, seed.SecondUserId, "Vote target"));
            await setupContext.SaveChangesAsync();
        }

        await using var context = database.CreateDbContext();
        var phraseId = await context.Phrases.Select(phrase => phrase.Id).SingleAsync();
        context.PhraseVotes.AddRange(
            CreatePhraseVote(seed.RoundId, phraseId, seed.FirstUserId),
            CreatePhraseVote(seed.RoundId, phraseId, seed.FirstUserId));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateGifVoteForPlayerAndRoundIsRejected()
    {
        var seed = await CreateRoundAsync();
        await using (var setupContext = database.CreateDbContext())
        {
            setupContext.GifSubmissions.Add(CreateGifSubmission(seed.RoundId, seed.SecondUserId));
            await setupContext.SaveChangesAsync();
        }

        await using var context = database.CreateDbContext();
        var submissionId = await context.GifSubmissions.Select(submission => submission.Id).SingleAsync();
        context.GifVotes.AddRange(
            CreateGifVote(seed.RoundId, submissionId, seed.FirstUserId),
            CreateGifVote(seed.RoundId, submissionId, seed.FirstUserId));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateGifSubmissionForPlayerAndRoundIsRejected()
    {
        var seed = await CreateRoundAsync();
        await using var context = database.CreateDbContext();
        context.GifSubmissions.AddRange(
            CreateGifSubmission(seed.RoundId, seed.FirstUserId),
            CreateGifSubmission(seed.RoundId, seed.FirstUserId));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<RoundSeed> CreateRoundAsync()
    {
        await database.ResetAsync();
        await using var context = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var firstUser = CreateUser("1001", now);
        var secondUser = CreateUser("1002", now);
        var game = new Game
        {
            Code = "ABCDE",
            HostUserId = firstUser.Id,
            HostUser = firstUser,
            TotalRounds = 3,
            CreatedAt = now
        };
        var round = new Round
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = 1,
            Phase = RoundPhase.PhraseSubmission,
            PhaseEndsAt = now.AddSeconds(30),
            StartedAt = now
        };

        context.AddRange(firstUser, secondUser, game, round);
        await context.SaveChangesAsync();
        return new(round.Id, firstUser.Id, secondUser.Id);
    }

    private static User CreateUser(string discordId, DateTimeOffset now) => new()
    {
        DiscordId = discordId,
        Username = $"user-{discordId}",
        DisplayName = $"User {discordId}",
        CreatedAt = now,
        UpdatedAt = now
    };

    private static Phrase CreatePhrase(Guid roundId, Guid userId, string text) => new()
    {
        RoundId = roundId,
        UserId = userId,
        Text = text,
        SubmittedAt = DateTimeOffset.UtcNow
    };

    private static PhraseVote CreatePhraseVote(Guid roundId, Guid phraseId, Guid userId) => new()
    {
        RoundId = roundId,
        PhraseId = phraseId,
        UserId = userId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static GifSubmission CreateGifSubmission(Guid roundId, Guid userId) => new()
    {
        RoundId = roundId,
        UserId = userId,
        Provider = "test",
        ExternalId = "gif-1",
        PreviewUrl = "https://example.test/preview.gif",
        MediaUrl = "https://example.test/media.gif",
        SourceUrl = "https://example.test/source",
        Attribution = "Test",
        SubmittedAt = DateTimeOffset.UtcNow
    };

    private static GifVote CreateGifVote(Guid roundId, Guid submissionId, Guid userId) => new()
    {
        RoundId = roundId,
        GifSubmissionId = submissionId,
        UserId = userId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed record RoundSeed(Guid RoundId, Guid FirstUserId, Guid SecondUserId);
}
