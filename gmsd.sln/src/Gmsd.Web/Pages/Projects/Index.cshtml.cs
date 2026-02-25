using Gmsd.Data.Dto.Models.Projects;
using Gmsd.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly IProjectService _projectService;

    public IndexModel(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public List<ProjectDto> Projects { get; set; } = new();

    public async Task OnGetAsync()
    {
        Projects = await _projectService.GetAllProjectsAsync();
    }
}
