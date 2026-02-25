namespace Gmsd.Web.Configuration;

/// <summary>
/// Configuration options for feature flags in the GMSD web application.
/// </summary>
public class FeatureFlagOptions
{
    /// <summary>
    /// Configuration section name for feature flags.
    /// </summary>
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Configuration for the ChatForwardUI feature flag.
    /// Controls whether the new chat-forward interface is enabled.
    /// </summary>
    public ChatForwardUIFlagOptions ChatForwardUI { get; set; } = new();
}

/// <summary>
/// Configuration options for the ChatForwardUI feature flag.
/// </summary>
public class ChatForwardUIFlagOptions
{
    /// <summary>
    /// Whether the feature flag is globally enabled.
    /// When false, all users see the old layout.
    /// When true, rollout rules apply.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Percentage of users (0-100) who should see the new UI when enabled.
    /// Uses a deterministic hash of the user's session for consistent assignment.
    /// </summary>
    public int RolloutPercentage { get; set; } = 0;

    /// <summary>
    /// Whether users can opt-in/opt-out of the feature via UI toggle.
    /// When true, user preferences override rollout assignment.
    /// </summary>
    public bool AllowUserOverride { get; set; } = true;
}
