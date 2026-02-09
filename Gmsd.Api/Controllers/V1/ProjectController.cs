using Gmsd.Data.Dto.Models.Projects;
using Gmsd.Data.Dto.Requests.Projects;
using Gmsd.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Api.Controllers.V1
{
    [Route("v1/projects")]

    public class ProjectController : GmsdController
    {
        private readonly IProjectService _projectService; // Interface for project data logic

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService; // Injects the project service
        }

        [HttpGet]
        public async Task<IActionResult> ListAllProjects()
        {
            var projectDtos = await _projectService.GetAllProjectsAsync(); // Asynchronously fetches all projects as DTOs
            return Ok(projectDtos); // Returns the DTOs with a 200 OK status
        }

        [HttpGet("search/{query}")]
        public async Task<IActionResult> ListProjects(string query)
        {
            var projectDtos = await _projectService.SearchProjectsAsync(query); // Searches for projects matching the query string
            return Ok(projectDtos); // Returns search results with a 200 OK status
        }

        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetProjectById(string projectId, CancellationToken cancellationToken)
        {
            var projectDto = await _projectService.GetProjectByIdAsync(projectId, cancellationToken);
            return Ok(projectDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject(
            [FromBody] ProjectCreateRequestDto request,
            CancellationToken cancellationToken)
        {
            var created = await _projectService.CreateProjectAsync(request, cancellationToken);

            return CreatedAtAction(nameof(GetProjectById), new { projectId = created.ProjectId }, created);
        }
    }
}
