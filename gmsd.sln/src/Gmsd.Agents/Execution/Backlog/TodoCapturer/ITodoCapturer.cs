namespace Gmsd.Agents.Execution.Backlog.TodoCapturer;

/// <summary>
/// Defines the contract for the TODO Capturer.
/// Captures TODOs from execution context to .aos/context/todos/ without affecting the cursor.
/// </summary>
public interface ITodoCapturer
{
    /// <summary>
    /// Captures a TODO item and writes it to the workspace.
    /// </summary>
    /// <param name="request">The capture request containing TODO details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The capture result with TODO ID and file path.</returns>
    Task<TodoCaptureResult> CaptureAsync(TodoCaptureRequest request, CancellationToken ct = default);
}
