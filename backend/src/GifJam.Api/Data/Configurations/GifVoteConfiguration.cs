using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class GifVoteConfiguration : IEntityTypeConfiguration<GifVote>
{
    public void Configure(EntityTypeBuilder<GifVote> builder)
    {
        builder.ToTable("gif_votes");
        builder.HasKey(vote => vote.Id);
        builder.HasIndex(vote => new { vote.RoundId, vote.UserId }).IsUnique();
        builder.HasOne(vote => vote.Round)
            .WithMany(round => round.GifVotes)
            .HasForeignKey(vote => vote.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.GifSubmission)
            .WithMany(submission => submission.Votes)
            .HasForeignKey(vote => vote.GifSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vote => vote.User)
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
