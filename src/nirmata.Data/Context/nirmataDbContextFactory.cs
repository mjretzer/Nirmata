using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace nirmata.Data.Context;

public sealed class nirmataDbContextFactory : IDesignTimeDbContextFactory<nirmataDbContext>
{
    public nirmataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<nirmataDbContext>();
        optionsBuilder.UseSqlite("Data Source=sqllitedb/nirmata.db");

        return new nirmataDbContext(optionsBuilder.Options);
    }
}
