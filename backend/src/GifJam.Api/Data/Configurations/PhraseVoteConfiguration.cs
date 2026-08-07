using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class PhraseVoteConfiguration : IEntityTypeConfiguration<PhraseVote>
{
    public void Configure(EntityTypeBuilder<PhraseVote> builder)
    {
        builder.ToTable("phrase_votes");
        builder.HasKey(vote => vote.Id);
        builder.HasIndex(vote => new { vote.RoundId, vote.UserId }).IsUnique();
        builder.HasOne(vote => vote.Round)
            .WithMany(round => round.PhraseVotes)
            .HasForeignKey(vote => vote.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.Phrase)
            .WithMany(phrase => phrase.Votes)
            .HasForeignKey(vote => vote.PhraseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.User)
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
