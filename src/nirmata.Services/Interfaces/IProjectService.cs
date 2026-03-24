using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;

namespace nirmata.Services.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> GetProjectByIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectResponseDto> CreateProjectAsync(ProjectCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ProjectDto>> GetAllProjectsAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProjectDto> Items, int TotalCount)> SearchProjectsAsync(ProjectSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<ProjectDto> UpdateProjectAsync(string projectId, ProjectUpdateRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteProjectAsync(string projectId, CancellationToken cancellationToken = default);
}
