using AutoMapper;
using Gmsd.Common.Exceptions;
using Gmsd.Data.Context;
using Gmsd.Data.Dto.Models.Projects;
using Gmsd.Data.Dto.Requests.Projects;
using Gmsd.Data.Entities.Projects;
using Gmsd.Data.Repositories;
using Gmsd.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Gmsd.Services.Implementations;

public class ProjectService : IProjectService // Service implementation for project data management
{
    private readonly GmsdDbContext _dbContext; // Injected database context instance
    private readonly IProjectRepository _projectRepository;
    private readonly IMapper _mapper;

    public ProjectService(GmsdDbContext dbContext, IProjectRepository projectRepository, IMapper mapper)
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

    public async Task<List<ProjectDto>> GetAllProjectsAsync() // Implements retrieval of all project records
    {
        var projects = await _dbContext.Projects.ToListAsync(); // Executes the query and returns a list of projects
        return _mapper.Map<List<ProjectDto>>(projects);
    }

    public async Task<List<ProjectDto>> SearchProjectsAsync(string query) // Implements search functionality
    {
        var projects = await _dbContext.Projects // Accesses the Projects table
            .Where(p => EF.Functions.Like(p.Name, $"%{query}%")) // Filters projects using a SQL LIKE comparison
            .ToListAsync(); // Executes the search and returns matching projects as a list

        return _mapper.Map<List<ProjectDto>>(projects);
    }
}
