using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gmsd.Data.Context;

public sealed class GmsdDbContextFactory : IDesignTimeDbContextFactory<GmsdDbContext>
{
    public GmsdDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GmsdDbContext>();
        optionsBuilder.UseSqlite("Data Source=sqllitedb/gmsd.db");

        return new GmsdDbContext(optionsBuilder.Options);
    }
}
