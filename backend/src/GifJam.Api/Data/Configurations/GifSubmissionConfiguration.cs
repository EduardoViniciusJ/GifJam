using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class GifSubmissionConfiguration : IEntityTypeConfiguration<GifSubmission>
{
    public void Configure(EntityTypeBuilder<GifSubmission> builder)
    {
        builder.ToTable("gif_submissions");
        builder.HasKey(submission => submission.Id);
        builder.HasIndex(submission => new { submission.RoundId, submission.UserId }).IsUnique();
        builder.Property(submission => submission.Provider).HasMaxLength(32).IsRequired();
        builder.Property(submission => submission.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(submission => submission.PreviewUrl).HasMaxLength(2048).IsRequired();
        builder.Property(submission => submission.MediaUrl).HasMaxLength(2048).IsRequired();
        builder.Property(submission => submission.SourceUrl).HasMaxLength(2048).IsRequired();
        builder.Property(submission => submission.Attribution).HasMaxLength(256).IsRequired();
        builder.HasOne(submission => submission.Round)
            .WithMany(round => round.GifSubmissions)
            .HasForeignKey(submission => submission.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(submission => submission.User)
            .WithMany()
            .HasForeignKey(submission => submission.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
