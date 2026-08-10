using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class RoundConfiguration : IEntityTypeConfiguration<Round>
{
    public void Configure(EntityTypeBuilder<Round> builder)
    {
        builder.ToTable("rounds");
        builder.HasKey(round => round.Id);
        builder.HasIndex(round => new { round.GameId, round.RoundNumber }).IsUnique();
        builder.HasIndex(round => new { round.Phase, round.PhaseEndsAt });
        builder.Property(round => round.Phase).HasConversion<string>().HasMaxLength(24);
        builder.HasOne(round => round.Game)
            .WithMany(game => game.Rounds)
            .HasForeignKey(round => round.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(round => round.SelectedPhrase)
            .WithMany()
            .HasForeignKey(round => round.SelectedPhraseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
