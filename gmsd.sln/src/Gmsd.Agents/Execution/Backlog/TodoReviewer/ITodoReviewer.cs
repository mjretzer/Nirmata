namespace Gmsd.Agents.Execution.Backlog.TodoReviewer;

/// <summary>
/// Defines the contract for the TODO Reviewer.
/// Reviews captured TODOs and promotes them to tasks or roadmap phases, or discards them.
/// </summary>
public interface ITodoReviewer
{
    /// <summary>
    /// Lists all TODOs from .aos/context/todos/ for review.
    /// </summary>
    /// <param name="request">The review request containing workspace path and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The review result with list of TODOs.</returns>
    Task<TodoReviewResult> ListTodosAsync(TodoReviewRequest request, CancellationToken ct = default);

    /// <summary>
    /// Promotes a TODO to a task spec.
    /// </summary>
    /// <param name="request">The promotion request containing TODO ID and promotion details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The promotion result with created task ID.</returns>
    Task<TodoPromotionResult> PromoteToTaskAsync(TodoPromotionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Promotes a TODO to a roadmap phase.
    /// </summary>
    /// <param name="request">The promotion request containing TODO ID and phase details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The promotion result with created phase ID.</returns>
    Task<TodoPromotionResult> PromoteToPhaseAsync(TodoPromotionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Discards a TODO (marks as discarded, optionally archives).
    /// </summary>
    /// <param name="request">The discard request containing TODO ID and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The discard result.</returns>
    Task<TodoDiscardResult> DiscardAsync(TodoDiscardRequest request, CancellationToken ct = default);
}
