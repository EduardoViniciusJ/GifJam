using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GifJam.Api.Data.Configurations;

public sealed class AuthExchangeCodeConfiguration : IEntityTypeConfiguration<AuthExchangeCode>
{
    public void Configure(EntityTypeBuilder<AuthExchangeCode> builder)
    {
        builder.ToTable("auth_exchange_codes");
        builder.HasKey(code => code.Id);
        builder.HasIndex(code => code.CodeHash).IsUnique();
        builder.HasIndex(code => code.ExpiresAt);
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
