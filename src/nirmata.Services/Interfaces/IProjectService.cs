using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;

namespace nirmata.Services.Interfaces;

public interface IProjectService // Interface for managing project operations
{
    Task<ProjectDto> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectResponseDto> CreateProjectAsync(ProjectCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ProjectDto>> GetAllProjectsAsync(); // Asynchronously retrieves all projects from the database
    Task<List<ProjectDto>> SearchProjectsAsync(string query); // Searches projects by name using a wildcard pattern
}
