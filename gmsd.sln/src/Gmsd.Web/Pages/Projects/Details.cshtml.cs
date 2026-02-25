using Gmsd.Data.Dto.Models.Projects;
using Gmsd.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Projects;

public class DetailsModel : PageModel
{
    private readonly IProjectService _projectService;

    public DetailsModel(IProjectService projectService)
    {
        _projectService = projectService;
    }

    public ProjectDto? Project { get; set; }
    public bool IsNotFound { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            IsNotFound = true;
            return Page();
        }

        try
        {
            Project = await _projectService.GetProjectByIdAsync(id);
            if (Project == null)
            {
                IsNotFound = true;
            }
        }
        catch
        {
            IsNotFound = true;
        }

        return Page();
    }
}
