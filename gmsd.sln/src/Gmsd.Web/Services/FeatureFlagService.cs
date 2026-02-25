using Gmsd.Web.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Gmsd.Web.Services;

/// <summary>
/// Service for evaluating feature flags based on configuration, rollout percentage, and user preferences.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Determines if the ChatForwardUI feature is enabled for the current user.
    /// </summary>
    /// <param name="sessionId">The user's session identifier for consistent rollout assignment.</param>
    /// <param name="userOverride">Optional user preference override (null = use rollout logic).</param>
    /// <returns>True if the new chat-forward UI should be shown.</returns>
    bool IsChatForwardUIEnabled(string sessionId, bool? userOverride = null);

    /// <summary>
    /// Gets the effective rollout percentage for the ChatForwardUI feature.
    /// </summary>
    int GetRolloutPercentage();

    /// <summary>
    /// Checks if user override is allowed for this feature.
    /// </summary>
    bool IsUserOverrideAllowed();
}

/// <summary>
/// Implementation of feature flag evaluation with support for percentage-based rollout.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagOptions _options;

    public FeatureFlagService(IOptions<FeatureFlagOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsChatForwardUIEnabled(string sessionId, bool? userOverride = null)
    {
        var flagConfig = _options.ChatForwardUI;

        // Feature is globally disabled
        if (!flagConfig.Enabled)
        {
            // Even when globally disabled, respect explicit opt-in if allowed
            return flagConfig.AllowUserOverride && userOverride == true;
        }

        // If user has explicitly set a preference and override is allowed, use it
        if (flagConfig.AllowUserOverride && userOverride.HasValue)
        {
            return userOverride.Value;
        }

        // If rollout is 0%, no one gets it unless explicitly opted in
        if (flagConfig.RolloutPercentage <= 0)
        {
            return false;
        }

        // If rollout is 100%, everyone gets it
        if (flagConfig.RolloutPercentage >= 100)
        {
            return true;
        }

        // Deterministic assignment based on session ID hash
        return IsInRolloutGroup(sessionId, flagConfig.RolloutPercentage);
    }

    /// <inheritdoc />
    public int GetRolloutPercentage()
    {
        return _options.ChatForwardUI.RolloutPercentage;
    }

    /// <inheritdoc />
    public bool IsUserOverrideAllowed()
    {
        return _options.ChatForwardUI.AllowUserOverride;
    }

    /// <summary>
    /// Determines if a session ID falls within the rollout percentage using a deterministic hash.
    /// This ensures the same user always gets the same experience.
    /// </summary>
    private static bool IsInRolloutGroup(string sessionId, int percentage)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            // Without a session ID, conservatively exclude from rollout
            return false;
        }

        // Generate a deterministic hash of the session ID
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        var hashValue = BitConverter.ToUInt32(hashBytes, 0);

        // Map to 0-99 range and check if within rollout percentage
        var bucket = hashValue % 100;
        return bucket < percentage;
    }
}
