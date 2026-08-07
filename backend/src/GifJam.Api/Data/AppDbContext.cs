using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<AuthExchangeCode> AuthExchangeCodes => Set<AuthExchangeCode>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();

    public DbSet<Round> Rounds => Set<Round>();

    public DbSet<Phrase> Phrases => Set<Phrase>();

    public DbSet<PhraseVote> PhraseVotes => Set<PhraseVote>();

    public DbSet<GifSubmission> GifSubmissions => Set<GifSubmission>();

    public DbSet<GifVote> GifVotes => Set<GifVote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
