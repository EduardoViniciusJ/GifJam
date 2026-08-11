using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class MatchmakingTicketConfiguration : IEntityTypeConfiguration<MatchmakingTicket>
{
    public void Configure(EntityTypeBuilder<MatchmakingTicket> builder)
    {
        builder.ToTable("matchmaking_tickets");
        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(ticket => new { ticket.BatchId, ticket.JoinedAt });
        builder.HasIndex(ticket => new { ticket.UserId, ticket.Status })
            .IsUnique()
            .HasFilter($"\"Status\" = '{MatchmakingTicketStatus.Waiting}'");
        builder.HasOne(ticket => ticket.Batch)
            .WithMany(batch => batch.Tickets)
            .HasForeignKey(ticket => ticket.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ticket => ticket.User)
            .WithMany()
            .HasForeignKey(ticket => ticket.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
