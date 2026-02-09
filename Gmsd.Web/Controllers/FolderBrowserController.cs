using System.Runtime.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Windows.Forms;

namespace Gmsd.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[SupportedOSPlatform("windows")]
public class FolderBrowserController : ControllerBase
{
    private readonly ILogger<FolderBrowserController> _logger;

    public FolderBrowserController(ILogger<FolderBrowserController> logger)
    {
        _logger = logger;
    }

    [HttpPost("select")]
    [SupportedOSPlatform("windows")]
    public IActionResult SelectFolder([FromBody] FolderBrowserRequest? request)
    {
        try
        {
            string? selectedPath = null;
            DialogResult dialogResult = DialogResult.Cancel;

            // Run the dialog on an STA thread
            var thread = new Thread(() =>
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = request?.Description ?? "Select a folder",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = request?.ShowNewFolderButton ?? true
                };

                if (!string.IsNullOrEmpty(request?.InitialPath) && Directory.Exists(request.InitialPath))
                {
                    dialog.SelectedPath = request.InitialPath;
                }

                dialogResult = dialog.ShowDialog();

                if (dialogResult == DialogResult.OK)
                {
                    selectedPath = dialog.SelectedPath;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (dialogResult == DialogResult.OK && !string.IsNullOrEmpty(selectedPath))
            {
                return Ok(new { success = true, path = selectedPath });
            }

            return Ok(new { success = false, path = (string?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing folder browser dialog");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

public class FolderBrowserRequest
{
    public string? Description { get; set; }
    public string? InitialPath { get; set; }
    public bool? ShowNewFolderButton { get; set; }
}
