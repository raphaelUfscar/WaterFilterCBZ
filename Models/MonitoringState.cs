namespace WaterFilterCBZ.Models
{
    /// <summary>
    /// The defined system-level monitoring states presented to the operator (RC-010 / SRS-C-006).
    /// Exactly one state is in effect at any time; it is derived deterministically by
    /// <see cref="MonitoringStateResolver.Resolve"/> from the connection and data-quality inputs.
    /// </summary>
    public enum MonitoringState
    {
        /// <summary>No serial connection; not attempting one.</summary>
        Disconnected,

        /// <summary>A connection attempt is in progress.</summary>
        Connecting,

        /// <summary>Connected and receiving fresh, in-spec, valid data.</summary>
        ConnectedHealthy,

        /// <summary>Connected but at least one sensor's data is stale (RC-002 / HAZ-002).</summary>
        Stale,

        /// <summary>Connected but at least one sensor reported a physically implausible (rejected) value (RC-008 / HAZ-001).</summary>
        InvalidValue,

        /// <summary>Connected but the frame parser is in a sustained error condition (HAZ-001/005). Detector lands with the parser-error supervisor.</summary>
        ParserError,

        /// <summary>Connected to a device whose identity/protocol version is incompatible (RC-003 / HAZ-003). Detector lands with RC-003.</summary>
        DeviceMismatch,

        /// <summary>The background acquisition/processing task terminated unexpectedly (RC-009 / HAZ-004).</summary>
        ProcessingFault,
    }

    /// <summary>
    /// Pure, deterministic derivation of the current <see cref="MonitoringState"/> and its
    /// human-readable description (RC-010 / SRS-C-006). Kept free of UI/threading concerns so the
    /// full state taxonomy can be unit-verified.
    /// </summary>
    public static class MonitoringStateResolver
    {
        /// <summary>
        /// Resolves the single effective monitoring state from the system inputs. Order of
        /// precedence (most to least severe): processing fault → not connected (connecting vs
        /// disconnected) → device mismatch → parser error → invalid value → stale → healthy.
        /// </summary>
        public static MonitoringState Resolve(
            bool isConnected,
            bool isConnecting,
            bool hasProcessingFault,
            bool hasDeviceMismatch,
            bool hasParserError,
            bool anyInvalidValue,
            bool anyStale)
        {
            // A dead processing task is the most serious condition and can coexist with a port
            // that still reports open, so it is evaluated first.
            if (hasProcessingFault)
                return MonitoringState.ProcessingFault;

            if (!isConnected)
                return isConnecting ? MonitoringState.Connecting : MonitoringState.Disconnected;

            // Connected: report the most severe active data-quality condition.
            if (hasDeviceMismatch)
                return MonitoringState.DeviceMismatch;
            if (hasParserError)
                return MonitoringState.ParserError;
            if (anyInvalidValue)
                return MonitoringState.InvalidValue;
            if (anyStale)
                return MonitoringState.Stale;

            return MonitoringState.ConnectedHealthy;
        }

        /// <summary>Short operator-facing label for a state (used by the status bar).</summary>
        public static string Describe(MonitoringState state) => state switch
        {
            MonitoringState.Disconnected => "Disconnected",
            MonitoringState.Connecting => "Connecting…",
            MonitoringState.ConnectedHealthy => "Connected — healthy",
            MonitoringState.Stale => "Stale data",
            MonitoringState.InvalidValue => "Invalid value rejected",
            MonitoringState.ParserError => "Parser error",
            MonitoringState.DeviceMismatch => "Device mismatch",
            MonitoringState.ProcessingFault => "Processing fault",
            _ => state.ToString(),
        };
    }
}
