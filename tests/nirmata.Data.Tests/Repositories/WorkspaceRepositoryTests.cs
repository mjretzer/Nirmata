using nirmata.Data.Context;
using nirmata.Data.Entities.Workspaces;
using nirmata.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace nirmata.Data.Tests.Repositories;

public class WorkspaceRepositoryTests
{
    private static nirmataDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<nirmataDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new nirmataDbContext(options);
    }

    [Fact]
    public async Task Add_And_GetByIdAsync_RoundTrips()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Path = "/projects/my-workspace",
            Name = "My Workspace"
        };

        // Act
        repo.Add(workspace);
        await repo.SaveChangesAsync();
        var result = await repo.GetByIdAsync(workspace.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workspace.Id, result.Id);
        Assert.Equal(workspace.Path, result.Path);
        Assert.Equal(workspace.Name, result.Name);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll_OrderedByLastOpenedAt_Descending()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var now = DateTimeOffset.UtcNow;
        var oldest = new Workspace { Id = Guid.NewGuid(), Path = "/a", Name = "A", LastOpenedAt = now.AddDays(-2) };
        var newest = new Workspace { Id = Guid.NewGuid(), Path = "/b", Name = "B", LastOpenedAt = now };
        var middle = new Workspace { Id = Guid.NewGuid(), Path = "/c", Name = "C", LastOpenedAt = now.AddDays(-1) };

        repo.Add(oldest);
        repo.Add(newest);
        repo.Add(middle);
        await repo.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(newest.Id, result[0].Id);
        Assert.Equal(middle.Id, result[1].Id);
        Assert.Equal(oldest.Id, result[2].Id);
    }

    [Fact]
    public async Task GetByPathAsync_ReturnsMatchingWorkspace()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var workspace = new Workspace { Id = Guid.NewGuid(), Path = "/projects/target", Name = "Target" };
        repo.Add(workspace);
        await repo.SaveChangesAsync();

        // Act
        var result = await repo.GetByPathAsync("/projects/target");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workspace.Id, result.Id);
    }

    [Fact]
    public async Task GetByPathAsync_ReturnsNull_WhenNoMatch()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);

        // Act
        var result = await repo.GetByPathAsync("/nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_RemovesWorkspace_FromRegistry()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var workspace = new Workspace { Id = Guid.NewGuid(), Path = "/to-delete", Name = "Delete Me" };
        repo.Add(workspace);
        await repo.SaveChangesAsync();

        // Act
        repo.Delete(workspace);
        await repo.SaveChangesAsync();
        var result = await repo.GetByIdAsync(workspace.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Update_PersistsNewPath_WithoutChangingId()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var id = Guid.NewGuid();
        var workspace = new Workspace { Id = id, Path = "/original", Name = "Workspace" };
        repo.Add(workspace);
        await repo.SaveChangesAsync();

        // Act
        workspace.Path = "/updated";
        repo.Update(workspace);
        await repo.SaveChangesAsync();
        var result = await repo.GetByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/updated", result.Path);
        Assert.Equal(id, result.Id);
    }

    [Fact]
    public async Task GetByHealthStatusAsync_ReturnsOnlyMatchingStatus()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var validatedAt = DateTimeOffset.UtcNow;
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/a", Name = "A", HealthStatus = "healthy", LastValidatedAt = validatedAt });
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/b", Name = "B", HealthStatus = "degraded", LastValidatedAt = validatedAt });
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/c", Name = "C", HealthStatus = "healthy", LastValidatedAt = validatedAt.AddMinutes(-1) });
        await repo.SaveChangesAsync();

        // Act
        var result = await repo.GetByHealthStatusAsync("healthy");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.Equal("healthy", w.HealthStatus));
    }

    [Fact]
    public async Task GetRecentlyValidatedAsync_ReturnsOnlyWithinWindow()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        var now = DateTimeOffset.UtcNow;
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/recent", Name = "Recent", LastValidatedAt = now.AddDays(-1) });
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/old", Name = "Old", LastValidatedAt = now.AddDays(-8) });
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/never", Name = "Never", LastValidatedAt = null });
        await repo.SaveChangesAsync();

        // Act
        var result = await repo.GetRecentlyValidatedAsync(7);

        // Assert
        Assert.Single(result);
        Assert.Equal("/recent", result[0].Path);
    }

    [Fact]
    public async Task SaveChangesAsync_ReturnsTrue_OnSuccess()
    {
        // Arrange
        using var context = CreateContext();
        var repo = new WorkspaceRepository(context);
        repo.Add(new Workspace { Id = Guid.NewGuid(), Path = "/save-test", Name = "Save Test" });

        // Act
        var saved = await repo.SaveChangesAsync();

        // Assert
        Assert.True(saved);
    }
}
