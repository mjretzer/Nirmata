using AutoMapper;
using Gmsd.Data.Dto.Model.Projects;
using Gmsd.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly IMapper _mapper;

        public ProjectController(IProjectService projectService, IMapper mapper)
        {
            _projectService = projectService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> ListAllProjects()
        {
            var projects = await _projectService.GetAllProjectsAsync();
            var projectDtos = _mapper.Map<List<ProjectDto>>(projects);
            return Ok(projectDtos);
        }

        [HttpGet("search/{query}")]
        public async Task<IActionResult> ListProjects(string query)
        {
            var projects = await _projectService.SearchProjectsAsync(query);
            var projectDtos = _mapper.Map<List<ProjectDto>>(projects);
            return Ok(projectDtos);
        }
    }
}
