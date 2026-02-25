using Gmsd.Data.Entities.Workspaces;
using Gmsd.Data.Repositories;
using Gmsd.Web.Models;
using Gmsd.Web.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.Services;

public class WorkspaceServiceTests
{
    private readonly Mock<IWorkspaceRepository> _workspaceRepositoryMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _workspaceRepositoryMock = new Mock<IWorkspaceRepository>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _service = new WorkspaceService(_workspaceRepositoryMock.Object, _httpContextAccessorMock.Object);
    }

    [Fact]
    public async Task ListWorkspacesAsync_ReturnsMappedDtos()
    {
        // Arrange
        var workspaces = new List<Workspace>
        {
            new() { Id = Guid.NewGuid(), Name = "W1", Path = "P1", LastOpenedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "W2", Path = "P2", LastOpenedAt = DateTimeOffset.UtcNow.AddHours(-1) }
        };
        _workspaceRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaces);

        // Act
        var result = await _service.ListWorkspacesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("W1", result[0].Name);
        Assert.Equal("W2", result[1].Name);
    }

    [Fact]
    public async Task OpenWorkspaceAsync_ExistingWorkspace_UpdatesLastOpenedAt()
    {
        // Arrange
        var path = Path.GetFullPath("test-path");
        var existing = new Workspace { Id = Guid.NewGuid(), Path = path, Name = "Test" };
        _workspaceRepositoryMock.Setup(r => r.GetByPathAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // Act
        var result = await _service.OpenWorkspaceAsync(path);

        // Assert
        Assert.NotNull(result.LastOpenedAt);
        _workspaceRepositoryMock.Verify(r => r.Update(existing), Times.Once);
    }
}
