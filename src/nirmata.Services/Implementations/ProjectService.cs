using AutoMapper;
using nirmata.Common.Exceptions;
using nirmata.Data.Context;
using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;
using nirmata.Data.Entities.Projects;
using nirmata.Data.Repositories;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

public class ProjectService : IProjectService // Service implementation for project data management
{
    private readonly nirmataDbContext _dbContext; // Injected database context instance
    private readonly IProjectRepository _projectRepository;
    private readonly IMapper _mapper;

    public ProjectService(nirmataDbContext dbContext, IProjectRepository projectRepository, IMapper mapper)
    {
        _dbContext = dbContext;
        _projectRepository = projectRepository;
        _mapper = mapper;
    }

    public async Task<ProjectDto> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
        {
            throw new NotFoundException($"Project '{projectId}' was not found.");
        }

        return _mapper.Map<ProjectDto>(project);
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(ProjectCreateRequestDto request, CancellationToken cancellationToken = default)
    {
        var project = _mapper.Map<Project>(request);
        project.ProjectId = Guid.NewGuid().ToString();

        _projectRepository.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProjectResponseDto>(project);
    }

    public async Task<List<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default)
    {
        var (items, _) = await _projectRepository.GetAllAsync(cancellationToken: cancellationToken);
        return _mapper.Map<List<ProjectDto>>(items);
    }

    public async Task<(IReadOnlyList<ProjectDto> Items, int TotalCount)> SearchProjectsAsync(ProjectSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await _projectRepository.GetAllAsync(
            request.SearchTerm,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return (_mapper.Map<IReadOnlyList<ProjectDto>>(items), totalCount);
    }

    public async Task<ProjectDto> UpdateProjectAsync(string projectId, ProjectUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
            throw new NotFoundException($"Project '{projectId}' was not found.");

        _mapper.Map(request, project);
        _projectRepository.Update(project);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProjectDto>(project);
    }

    public async Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var deleted = await _projectRepository.DeleteAsync(projectId, cancellationToken);

        if (!deleted)
            throw new NotFoundException($"Project '{projectId}' was not found.");

        await _projectRepository.SaveChangesAsync(cancellationToken);
    }
}
