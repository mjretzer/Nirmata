using Gmsd.Data.Entities.Projects; // Importing Project and Step models
using Microsoft.EntityFrameworkCore; // Importing EF Core features

namespace Gmsd.Data.Context; // Defining the namespace for the database context

public class GmsdDbContext : DbContext // Main class for interacting with the database
{
    public DbSet<Project> Projects { get; set; } // Table mapping for Project entities
    public DbSet<Step> Steps { get; set; } // Table mapping for Step entities

    public GmsdDbContext(DbContextOptions<GmsdDbContext> options) // Constructor for Dependency Injection
        : base(options) // Passing options to the base DbContext class
    {
    }

    // Default constructor for design-time tools
    public GmsdDbContext() // Parameterless constructor required by some EF Core tools
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) // Configuring database connection if not done via DI
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=sqllitedb/gmsd.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) // Configuring model relationships and constraints
    {
        base.OnModelCreating(modelBuilder); // Calling base implementation for default configurations

        // Configure Project -> Steps relationship
        modelBuilder.Entity<Project>() // Starting configuration for the Project entity
            .HasMany(p => p.Steps) // Defining a one-to-many relationship: one Project has many Steps
            .WithOne(s => s.Project) // Defining the inverse relationship: one Step belongs to one Project
            .HasForeignKey(s => s.ProjectId) // Specifying ProjectId as the foreign key in the Steps table
            .OnDelete(DeleteBehavior.Cascade); // Enabling cascade delete: deleting a Project deletes its associated Steps

        // Seed initial project data
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                ProjectId = "proj-sample-001",
                Name = "Sample Web Application"
            },
            new Project
            {
                ProjectId = "proj-sample-002",
                Name = "API Migration Project"
            },
            new Project
            {
                ProjectId = "proj-sample-003",
                Name = "Database Optimization Initiative"
            }
        );
    }
}
