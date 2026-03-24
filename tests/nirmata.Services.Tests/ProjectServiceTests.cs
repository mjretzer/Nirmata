using AutoMapper;
using nirmata.Common.Exceptions;
using nirmata.Data.Context;
using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;
using nirmata.Data.Entities.Projects;
using nirmata.Data.Mapping;
using nirmata.Data.Repositories;
using nirmata.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace nirmata.Services.Tests;

public class ProjectServiceTests : IDisposable
{
    private readonly Mock<IProjectRepository> _mockRepository;
    private readonly nirmataDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _mockRepository = new Mock<IProjectRepository>();

        var options = new DbContextOptionsBuilder<nirmataDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new nirmataDbContext(options);

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();

        _service = new ProjectService(_dbContext, _mockRepository.Object, _mapper);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ReturnsMappedDto_WhenProjectExists()
    {
        // Arrange
        var projectId = "test-id";
        var project = new Project
        {
            ProjectId = projectId,
            Name = "Test Project"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var result = await _service.GetProjectByIdAsync(projectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("Test Project", result.Name);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ThrowsNotFoundException_WhenProjectNotFound()
    {
        // Arrange
        var projectId = "non-existent-id";
        _mockRepository.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetProjectByIdAsync(projectId));
        Assert.Contains(projectId, exception.Message);
    }

    [Fact]
    public async Task CreateProjectAsync_PersistsEntityAndReturnsResponseDto()
    {
        // Arrange
        var request = new ProjectCreateRequestDto
        {
            Name = "New Project"
        };

        _mockRepository.Setup(r => r.Add(It.IsAny<Project>()));

        // Act
        var result = await _service.CreateProjectAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Project", result.Name);
        Assert.NotNull(result.ProjectId);

        _mockRepository.Verify(r => r.Add(It.IsAny<Project>()), Times.Once);
    }

    [Fact]
    public async Task GetAllProjectsAsync_ReturnsAllProjectsAsDtos()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { ProjectId = "1", Name = "Project 1" },
            new() { ProjectId = "2", Name = "Project 2" }
        };

        foreach (var project in projects)
        {
            _dbContext.Projects.Add(project);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAllProjectsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.ProjectId == "1" && p.Name == "Project 1");
        Assert.Contains(result, p => p.ProjectId == "2" && p.Name == "Project 2");
    }

    [Fact]
    public async Task SearchProjectsAsync_FiltersAndReturnsMatchingDtos()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { ProjectId = "1", Name = "Alpha Project" },
            new() { ProjectId = "2", Name = "Beta Project" },
            new() { ProjectId = "3", Name = "Gamma Test" }
        };

        foreach (var project in projects)
        {
            _dbContext.Projects.Add(project);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var (items, _) = await _service.SearchProjectsAsync(new ProjectSearchRequestDto { SearchTerm = "Project" });

        // Assert
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, p => p.Name == "Alpha Project");
        Assert.Contains(items, p => p.Name == "Beta Project");
        Assert.DoesNotContain(items, p => p.Name == "Gamma Test");
    }

    [Fact]
    public async Task SearchProjectsAsync_ReturnsEmptyList_WhenNoMatches()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { ProjectId = "1", Name = "Alpha Project" }
        };

        foreach (var project in projects)
        {
            _dbContext.Projects.Add(project);
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var (items, _) = await _service.SearchProjectsAsync(new ProjectSearchRequestDto { SearchTerm = "NonExistent" });

        // Assert
        Assert.NotNull(items);
        Assert.Empty(items);
    }
}
