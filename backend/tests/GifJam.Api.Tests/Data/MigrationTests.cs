using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GifJam.Api.Tests.Data;

[Collection(PostgresTestGroup.Name)]
public sealed class MigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task InitialMigrationCanBeAppliedRolledBackAndReapplied()
    {
        await database.ResetAsync();
        await using var context = database.CreateDbContext();
        var migrator = context.Database.GetService<IMigrator>();

        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());

        await migrator.MigrateAsync(Migration.InitialDatabase);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());

        await context.Database.MigrateAsync();
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }
}
