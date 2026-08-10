using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class PhraseConfiguration : IEntityTypeConfiguration<Phrase>
{
    public void Configure(EntityTypeBuilder<Phrase> builder)
    {
        builder.ToTable("phrases");
        builder.HasKey(phrase => phrase.Id);
        builder.HasIndex(phrase => new { phrase.RoundId, phrase.UserId }).IsUnique();
        builder.Property(phrase => phrase.Text).HasMaxLength(180).IsRequired();
        builder.HasOne(phrase => phrase.Round)
            .WithMany(round => round.Phrases)
            .HasForeignKey(phrase => phrase.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(phrase => phrase.User)
            .WithMany()
            .HasForeignKey(phrase => phrase.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
