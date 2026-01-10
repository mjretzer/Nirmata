using Gmsd.Data.Model.Projects;
using Microsoft.EntityFrameworkCore;

namespace Gmsd.Data;

public class GmsdDbContext : DbContext
{
    public GmsdDbContext(DbContextOptions<GmsdDbContext> options)
        : base(options)
    {
    }

    // Default constructor for design-time tools
    public GmsdDbContext()
    {
    }

    public DbSet<Project> Projects { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entity mappings here
    }
}
