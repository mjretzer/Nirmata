using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Persistence.State;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Runs;

public class IndexModel : PageModel
{
    private readonly IRunRepository _runRepository;

    public IndexModel(IRunRepository runRepository)
    {
        _runRepository = runRepository;
    }

    public List<RunResponse> Runs { get; set; } = new();

    public async Task OnGetAsync()
    {
        var runs = await _runRepository.ListAsync();
        Runs = runs.OrderByDescending(r => r.StartedAt).ToList();
    }
}
