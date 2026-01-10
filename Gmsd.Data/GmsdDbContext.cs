using Gmsd.Data.Model.Projects;
using Microsoft.EntityFrameworkCore;

namespace Gmsd.Data;

public class GmsdDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<Step> Steps { get; set; }

    public GmsdDbContext(DbContextOptions<GmsdDbContext> options)
        : base(options)
    {
    }

    // Default constructor for design-time tools
    public GmsdDbContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Project -> Steps relationship
        modelBuilder.Entity<Project>()
            .HasMany(p => p.Steps)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
