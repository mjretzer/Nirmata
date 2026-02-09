using System.Text.Json;

namespace Gmsd.Web.Models;

public class JsonViewerModel
{
    public string JsonContent { get; set; } = string.Empty;
    public string FileName { get; set; } = "data.json";
    public bool ShowToolbar { get; set; } = true;
    public bool AllowDownload { get; set; } = false;
    public string? DownloadUrl { get; set; }

    public bool IsValidJson
    {
        get
        {
            try
            {
                JsonSerializer.Deserialize<JsonElement>(JsonContent);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public string FormattedJson
    {
        get
        {
            try
            {
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(JsonContent);
                return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return JsonContent;
            }
        }
    }
}
