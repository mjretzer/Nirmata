using nirmata.Data.Entities.Chat;
using nirmata.Data.Entities.Projects; // Importing Project and Step models
using nirmata.Data.Entities.Workspaces;
using Microsoft.EntityFrameworkCore; // Importing EF Core features

namespace nirmata.Data.Context; // Defining the namespace for the database context

public class nirmataDbContext : DbContext // Main class for interacting with the database
{
    public DbSet<Project> Projects { get; set; } // Table mapping for Project entities
    public DbSet<Step> Steps { get; set; } // Table mapping for Step entities
    public DbSet<Workspace> Workspaces { get; set; } // Table mapping for Workspace entities
    public DbSet<ChatMessage> ChatMessages { get; set; } // Table mapping for chat history

    public nirmataDbContext(DbContextOptions<nirmataDbContext> options) // Constructor for Dependency Injection
        : base(options) // Passing options to the base DbContext class
    {
    }

    // Default constructor for design-time tools
    public nirmataDbContext() // Parameterless constructor required by some EF Core tools
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) // Configuring database connection if not done via DI
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=sqllitedb/nirmata.db");
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

        // Configure Workspace -> ChatMessages relationship
        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.Workspace)
            .WithMany()
            .HasForeignKey(cm => cm.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Database-level defaults for JSON array columns (must match the migration DDL)
        modelBuilder.Entity<ChatMessage>()
            .Property(cm => cm.ArtifactsJson)
            .HasDefaultValue("[]");

        modelBuilder.Entity<ChatMessage>()
            .Property(cm => cm.LogsJson)
            .HasDefaultValue("[]");

        // Index for workspace-scoped thread queries (ordered by timestamp)
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(cm => new { cm.WorkspaceId, cm.Timestamp });


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
