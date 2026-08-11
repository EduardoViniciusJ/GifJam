using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class MatchmakingBatchConfiguration : IEntityTypeConfiguration<MatchmakingBatch>
{
    public void Configure(EntityTypeBuilder<MatchmakingBatch> builder)
    {
        builder.ToTable("matchmaking_batches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(batch => new { batch.Status, batch.DeadlineAt });
        builder.HasOne(batch => batch.Game)
            .WithMany()
            .HasForeignKey(batch => batch.GameId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
