using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gmsd.Web.Helpers;

/// <summary>
/// Toast notification helper for Razor Pages.
/// Provides methods to queue toast notifications that will be displayed on the next page load.
/// </summary>
public static class ToastHelper
{
    private const string ToastKey = "Gmsd_Toasts";

    /// <summary>
    /// Adds a toast notification to TempData.
    /// </summary>
    /// <param name="pageModel">The PageModel instance</param>
    /// <param name="message">The toast message</param>
    /// <param name="type">Toast type: success, error, warning, info</param>
    /// <param name="title">Optional custom title</param>
    /// <param name="duration">Duration in milliseconds (default: 5000, null for persistent)</param>
    public static void AddToast(this PageModel pageModel, string message, string type = "info", string? title = null, int? duration = null)
    {
        var toasts = GetToasts(pageModel);
        toasts.Add(new ToastMessage
        {
            Message = message,
            Type = type,
            Title = title,
            Duration = duration
        });
        SaveToasts(pageModel, toasts);
    }

    /// <summary>
    /// Adds a success toast notification.
    /// </summary>
    public static void ToastSuccess(this PageModel pageModel, string message, string? title = null, int? duration = null)
    {
        AddToast(pageModel, message, "success", title ?? "Success", duration);
    }

    /// <summary>
    /// Adds an error toast notification.
    /// </summary>
    public static void ToastError(this PageModel pageModel, string message, string? title = null, int? duration = null)
    {
        AddToast(pageModel, message, "error", title ?? "Error", duration ?? 8000);
    }

    /// <summary>
    /// Adds a warning toast notification.
    /// </summary>
    public static void ToastWarning(this PageModel pageModel, string message, string? title = null, int? duration = null)
    {
        AddToast(pageModel, message, "warning", title ?? "Warning", duration);
    }

    /// <summary>
    /// Adds an info toast notification.
    /// </summary>
    public static void ToastInfo(this PageModel pageModel, string message, string? title = null, int? duration = null)
    {
        AddToast(pageModel, message, "info", title ?? "Info", duration);
    }

    /// <summary>
    /// Adds a validation failure toast with error details.
    /// </summary>
    public static void ToastValidationFailure(this PageModel pageModel, string context, IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        var message = errorList.Count == 1
            ? errorList.First()
            : $"{errorList.Count} validation errors in {context}";

        AddToast(pageModel, message, "error", $"Validation Failed: {context}", 10000);
    }

    /// <summary>
    /// Adds a run completion toast notification.
    /// </summary>
    public static void ToastRunCompleted(this PageModel pageModel, string runId, string status, string? summary = null)
    {
        var type = status.ToLower() switch
        {
            "success" or "completed" or "passed" => "success",
            "failed" or "error" => "error",
            "paused" or "warning" => "warning",
            _ => "info"
        };

        var message = summary ?? $"Run {runId} {status}";
        var title = status switch
        {
            "success" or "completed" or "passed" => "Run Completed",
            "failed" or "error" => "Run Failed",
            "paused" => "Run Paused",
            _ => "Run Status"
        };

        AddToast(pageModel, message, type, title, type == "error" ? 8000 : 6000);
    }

    /// <summary>
    /// Adds a lock conflict toast notification.
    /// </summary>
    public static void ToastLockConflict(this PageModel pageModel, string lockOwner, string operation)
    {
        var message = $"Cannot {operation}: Workspace is locked by {lockOwner}";
        AddToast(pageModel, message, "warning", "Lock Conflict", null); // Persistent for lock conflicts
    }

    /// <summary>
    /// Retrieves and clears pending toasts from TempData.
    /// </summary>
    internal static List<ToastMessage> GetAndClearToasts(PageModel pageModel)
    {
        var toasts = GetToasts(pageModel);
        pageModel.TempData.Remove(ToastKey);
        return toasts;
    }

    /// <summary>
    /// Gets pending toasts as JSON for client-side rendering.
    /// </summary>
    public static string GetToastsJson(PageModel pageModel)
    {
        var toasts = GetAndClearToasts(pageModel);
        return JsonSerializer.Serialize(toasts);
    }

    private static List<ToastMessage> GetToasts(PageModel pageModel)
    {
        if (pageModel.TempData.TryGetValue(ToastKey, out var value) && value is string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<ToastMessage>>(json) ?? new List<ToastMessage>();
            }
            catch
            {
                return new List<ToastMessage>();
            }
        }
        return new List<ToastMessage>();
    }

    private static void SaveToasts(PageModel pageModel, List<ToastMessage> toasts)
    {
        pageModel.TempData[ToastKey] = JsonSerializer.Serialize(toasts);
    }
}

/// <summary>
/// Represents a toast message for serialization.
/// </summary>
public class ToastMessage
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public string? Title { get; set; }
    public int? Duration { get; set; }
}
