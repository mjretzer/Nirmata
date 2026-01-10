using Gmsd.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProjectController : ControllerBase
    {

        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<IActionResult> ListAllProjects()
        {
            var projects = await _projectService.GetAllProjectsAsync();


            return Ok(projects);
        }


        [HttpGet("search/{query}")]
        public async Task<IActionResult> ListProjects(string query)
        {
            var projects = await _projectService.SearchProjectsAsync(query);
            return Ok(projects);
        }


       


    }
}
