namespace nirmata.Agents.Execution.ControlPlane.Chat;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages conversation history with token budget enforcement and sliding-window optimization.
/// </summary>
public sealed class ConversationHistoryManager
{
    private readonly List<ConversationTurn> _history = new();
    private readonly int _maxTokenBudget;
    private readonly int _minContextTurns;
    private int _currentTokenCount = 0;

    /// <summary>
    /// Creates a new conversation history manager.
    /// </summary>
    /// <param name="maxTokenBudget">Maximum tokens allowed for conversation history (default 4000)</param>
    /// <param name="minContextTurns">Minimum number of turns to keep regardless of token budget (default 3)</param>
    public ConversationHistoryManager(int maxTokenBudget = 4000, int minContextTurns = 3)
    {
        _maxTokenBudget = maxTokenBudget;
        _minContextTurns = minContextTurns;
    }

    /// <summary>
    /// Adds a turn to the conversation history.
    /// </summary>
    public void AddTurn(string role, string content, int estimatedTokens)
    {
        var turn = new ConversationTurn
        {
            Role = role,
            Content = content,
            EstimatedTokens = estimatedTokens,
            Timestamp = DateTimeOffset.UtcNow
        };

        _history.Add(turn);
        _currentTokenCount += estimatedTokens;

        // Enforce token budget by removing oldest turns
        EnforceTokenBudget();
    }

    /// <summary>
    /// Gets the conversation history formatted for LLM context.
    /// </summary>
    public string GetFormattedHistory()
    {
        if (_history.Count == 0)
            return string.Empty;

        var lines = _history.Select(turn => $"{turn.Role}: {turn.Content}");
        return string.Join("\n\n", lines);
    }

    /// <summary>
    /// Gets the current token count of the history.
    /// </summary>
    public int GetCurrentTokenCount() => _currentTokenCount;

    /// <summary>
    /// Gets the remaining token budget.
    /// </summary>
    public int GetRemainingTokenBudget() => Math.Max(0, _maxTokenBudget - _currentTokenCount);

    /// <summary>
    /// Gets the number of turns in history.
    /// </summary>
    public int GetTurnCount() => _history.Count;

    /// <summary>
    /// Checks if adding content would exceed the token budget.
    /// </summary>
    public bool WouldExceedBudget(int estimatedTokens)
    {
        return _currentTokenCount + estimatedTokens > _maxTokenBudget;
    }

    /// <summary>
    /// Clears all history.
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentTokenCount = 0;
    }

    /// <summary>
    /// Gets the last N turns.
    /// </summary>
    public IReadOnlyList<ConversationTurn> GetLastTurns(int count)
    {
        return _history.TakeLast(count).ToList();
    }

    /// <summary>
    /// Enforces the token budget by removing oldest turns when necessary.
    /// </summary>
    private void EnforceTokenBudget()
    {
        while (_currentTokenCount > _maxTokenBudget && _history.Count > _minContextTurns)
        {
            var oldestTurn = _history[0];
            _history.RemoveAt(0);
            _currentTokenCount -= oldestTurn.EstimatedTokens;
        }
    }

    /// <summary>
    /// Represents a single turn in the conversation.
    /// </summary>
    public sealed class ConversationTurn
    {
        /// <summary>
        /// The role (user, assistant, system).
        /// </summary>
        public required string Role { get; init; }

        /// <summary>
        /// The content of the turn.
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Estimated token count for this turn.
        /// </summary>
        public required int EstimatedTokens { get; init; }

        /// <summary>
        /// When this turn was added.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }
    }
}
