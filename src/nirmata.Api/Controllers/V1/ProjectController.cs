using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace nirmata.Api.Controllers.V1
{
    [Route("v1/projects")]
    public class ProjectController : nirmataController
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        /// <summary>
        /// Returns a paginated list of projects with optional search filtering.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListAllProjects(
            [FromQuery] ProjectSearchRequestDto request,
            CancellationToken cancellationToken)
        {
            var (items, totalCount) = await _projectService.SearchProjectsAsync(request, cancellationToken);
            return Ok(new { items, totalCount, pageNumber = request.PageNumber, pageSize = request.PageSize });
        }

        /// <summary>
        /// Searches for projects by search term with pagination support.
        /// </summary>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProjects(
            [FromQuery] ProjectSearchRequestDto request,
            CancellationToken cancellationToken)
        {
            var (items, totalCount) = await _projectService.SearchProjectsAsync(request, cancellationToken);
            return Ok(new { items, totalCount, pageNumber = request.PageNumber, pageSize = request.PageSize });
        }

        /// <summary>
        /// Searches for projects by name using a route parameter.
        /// </summary>
        [HttpGet("search/{query}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProjectsByQuery(
            [FromRoute][MaxLength(200)] string query,
            [FromQuery] ProjectSearchRequestDto request,
            CancellationToken cancellationToken)
        {
            var serviceRequest = new ProjectSearchRequestDto
            {
                SearchTerm = query,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };

            var (items, totalCount) = await _projectService.SearchProjectsAsync(serviceRequest, cancellationToken);
            return Ok(new { items, totalCount, pageNumber = serviceRequest.PageNumber, pageSize = serviceRequest.PageSize });
        }

        /// <summary>
        /// Retrieves a project by its unique identifier.
        /// </summary>
        [HttpGet("{projectId}")]
        [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProjectById(string projectId, CancellationToken cancellationToken)
        {
            var projectDto = await _projectService.GetProjectByIdAsync(projectId, cancellationToken);
            return Ok(projectDto);
        }

        /// <summary>
        /// Creates a new project.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ProjectResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProject(
            [FromBody] ProjectCreateRequestDto request,
            CancellationToken cancellationToken)
        {
            var created = await _projectService.CreateProjectAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetProjectById), new { projectId = created.ProjectId }, created);
        }

        /// <summary>
        /// Updates an existing project.
        /// </summary>
        [HttpPut("{projectId}")]
        [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProject(
            string projectId,
            [FromBody] ProjectUpdateRequestDto request,
            CancellationToken cancellationToken)
        {
            var updated = await _projectService.UpdateProjectAsync(projectId, request, cancellationToken);
            return Ok(updated);
        }

        /// <summary>
        /// Deletes a project by its unique identifier.
        /// </summary>
        [HttpDelete("{projectId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProject(
            string projectId,
            CancellationToken cancellationToken)
        {
            await _projectService.DeleteProjectAsync(projectId, cancellationToken);
            return NoContent();
        }
    }
}
