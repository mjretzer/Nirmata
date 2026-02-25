namespace Gmsd.Web.Models.Streaming;

/// <summary>
/// Defines event versioning constants and strategies for backward compatibility.
/// Ensures events can evolve while maintaining compatibility with older clients.
/// </summary>
public static class EventVersioning
{
    /// <summary>
    /// Current protocol version for streaming events
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Minimum supported protocol version
    /// </summary>
    public const int MinimumSupportedVersion = 1;

    /// <summary>
    /// Version history and migration notes
    /// </summary>
    public static class Versions
    {
        /// <summary>
        /// Version 1: Initial streaming event protocol
        /// - Basic event envelope with type, timestamp, payload
        /// - Limited event types (IntentClassified, GateSelected, ToolCall, ToolResult, PhaseLifecycle, AssistantDelta, AssistantFinal, RunLifecycle, Error)
        /// - No correlation ID or sequence number support
        /// </summary>
        public const int V1 = 1;

        /// <summary>
        /// Version 2: Enhanced streaming protocol with observability
        /// - Added correlation ID for request tracing
        /// - Added sequence number for event ordering
        /// - Expanded event types for tool calling loop visibility
        /// - Added comprehensive error context
        /// - Introduced event versioning metadata
        /// </summary>
        public const int V2 = 2;
    }

    /// <summary>
    /// Determines if a version is supported
    /// </summary>
    public static bool IsVersionSupported(int version)
    {
        return version >= MinimumSupportedVersion && version <= CurrentVersion;
    }

    /// <summary>
    /// Gets the migration path from one version to another
    /// </summary>
    public static MigrationPath GetMigrationPath(int fromVersion, int toVersion)
    {
        if (!IsVersionSupported(fromVersion) || !IsVersionSupported(toVersion))
            return new MigrationPath { IsSupported = false };

        if (fromVersion == toVersion)
            return new MigrationPath { IsSupported = true, RequiresMigration = false };

        return new MigrationPath
        {
            IsSupported = true,
            RequiresMigration = true,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Steps = GenerateMigrationSteps(fromVersion, toVersion)
        };
    }

    /// <summary>
    /// Generates the migration steps between versions
    /// </summary>
    private static List<string> GenerateMigrationSteps(int fromVersion, int toVersion)
    {
        var steps = new List<string>();

        if (fromVersion < 2 && toVersion >= 2)
        {
            steps.Add("Add correlationId field to event envelope");
            steps.Add("Add sequenceNumber field to event envelope");
            steps.Add("Expand event type enumeration with tool calling loop events");
            steps.Add("Add event versioning metadata");
        }

        return steps;
    }
}

/// <summary>
/// Represents a migration path between protocol versions
/// </summary>
public class MigrationPath
{
    /// <summary>
    /// Whether the migration is supported
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// Whether migration is required (versions differ)
    /// </summary>
    public bool RequiresMigration { get; set; }

    /// <summary>
    /// Source version
    /// </summary>
    public int FromVersion { get; set; }

    /// <summary>
    /// Target version
    /// </summary>
    public int ToVersion { get; set; }

    /// <summary>
    /// Steps required for migration
    /// </summary>
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// Handles backward compatibility for streaming events
/// </summary>
public class BackwardCompatibilityHandler
{
    /// <summary>
    /// Upgrades a V1 event to V2 format
    /// </summary>
    public static StreamingEvent UpgradeV1ToV2(StreamingEvent v1Event)
    {
        // V1 events are automatically compatible with V2 format
        // Just ensure the new fields have sensible defaults
        if (v1Event.CorrelationId == null)
            v1Event.CorrelationId = Guid.NewGuid().ToString("N");

        if (v1Event.SequenceNumber == null)
            v1Event.SequenceNumber = 0;

        return v1Event;
    }

    /// <summary>
    /// Downgrades a V2 event to V1 format for legacy clients
    /// </summary>
    public static StreamingEvent DowngradeV2ToV1(StreamingEvent v2Event)
    {
        // Create a V1-compatible copy by removing V2-specific fields
        var v1Event = new StreamingEvent
        {
            Id = v2Event.Id,
            Type = v2Event.Type,
            Timestamp = v2Event.Timestamp,
            Payload = v2Event.Payload
            // Intentionally omit CorrelationId and SequenceNumber for V1 compatibility
        };

        return v1Event;
    }

    /// <summary>
    /// Checks if an event type is supported in a given version
    /// </summary>
    public static bool IsEventTypeSupportedInVersion(StreamingEventType eventType, int version)
    {
        // V1 supported events
        var v1Events = new[]
        {
            StreamingEventType.IntentClassified,
            StreamingEventType.GateSelected,
            StreamingEventType.ToolCall,
            StreamingEventType.ToolResult,
            StreamingEventType.PhaseLifecycle,
            StreamingEventType.AssistantDelta,
            StreamingEventType.AssistantFinal,
            StreamingEventType.RunLifecycle,
            StreamingEventType.Error
        };

        if (version == EventVersioning.Versions.V1)
            return v1Events.Contains(eventType);

        // V2 supports all event types
        if (version == EventVersioning.Versions.V2)
            return true;

        return false;
    }
}
