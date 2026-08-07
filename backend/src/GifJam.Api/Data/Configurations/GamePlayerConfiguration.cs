using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class GamePlayerConfiguration : IEntityTypeConfiguration<GamePlayer>
{
    public void Configure(EntityTypeBuilder<GamePlayer> builder)
    {
        builder.ToTable("game_players");
        builder.HasKey(player => new { player.GameId, player.UserId });
        builder.HasIndex(player => player.UserId);
        builder.HasOne(player => player.Game)
            .WithMany(game => game.Players)
            .HasForeignKey(player => player.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(player => player.User)
            .WithMany(user => user.Games)
            .HasForeignKey(player => player.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
