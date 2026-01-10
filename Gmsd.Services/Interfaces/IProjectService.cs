using Gmsd.Data.Model.Projects;

namespace Gmsd.Services.Interfaces;

public interface IProjectService
{
    Task<List<Project>> GetAllProjectsAsync();
    Task<List<Project>> SearchProjectsAsync(string query);
}
