using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");
        builder.HasKey(game => game.Id);
        builder.HasIndex(game => game.Code).IsUnique();
        builder.HasIndex(game => new { game.Status, game.CreatedAt });
        builder.Property(game => game.Code).HasMaxLength(5).IsFixedLength().IsRequired();
        builder.Property(game => game.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(game => game.Mode).HasConversion<string>().HasMaxLength(24);
        builder.Property(game => game.Version).IsConcurrencyToken();
        builder.HasOne(game => game.HostUser)
            .WithMany(user => user.HostedGames)
            .HasForeignKey(game => game.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
