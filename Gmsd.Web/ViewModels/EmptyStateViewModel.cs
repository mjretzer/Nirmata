namespace Gmsd.Web.ViewModels;

public class EmptyStateViewModel
{
    public string Title { get; set; } = "No items found";
    public string? Message { get; set; }
    public string? Icon { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}
