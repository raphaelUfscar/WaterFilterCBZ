using Serilog;

namespace WaterFilterCBZ.Models
{
    /// <summary>
    /// Result of two-tier value validation for a sensor reading (RC-008 / SRS-C-003).
    /// </summary>
    public enum SensorValidationState
    {
        /// <summary>Value is within the operating specification.</summary>
        Normal,

        /// <summary>Value is physically plausible but outside the operating specification (flagged, still displayed).</summary>
        OutOfSpec,

        /// <summary>Value is physically implausible / corrupt (NaN, infinite, or outside the sensor's physical range); rejected.</summary>
        Invalid
    }

    /// <summary>
    /// Describes a monitored process parameter: its name, unit, operating specification
    /// band, and physically plausible range. Used to validate incoming sensor values.
    /// Ranges are defaults for pharmaceutical / medical purified-water monitoring (OAI-003)
    /// and are intended to be confirmed against the device specification.
    /// </summary>
    public sealed class SensorParameter
    {
        public string Name { get; }
        public string Unit { get; }

        /// <summary>Lower bound of the operating specification (inclusive).</summary>
        public double OperatingMin { get; }

        /// <summary>Upper bound of the operating specification (inclusive).</summary>
        public double OperatingMax { get; }

        /// <summary>Lower bound of the physically plausible range (inclusive).</summary>
        public double PhysicalMin { get; }

        /// <summary>Upper bound of the physically plausible range (inclusive).</summary>
        public double PhysicalMax { get; }

        public SensorParameter(
            string name,
            string unit,
            double operatingMin,
            double operatingMax,
            double physicalMin,
            double physicalMax)
        {
            Name = name;
            Unit = unit;
            OperatingMin = operatingMin;
            OperatingMax = operatingMax;
            PhysicalMin = physicalMin;
            PhysicalMax = physicalMax;
        }

        /// <summary>
        /// True when the value is a finite number inside the physical range. A value failing
        /// this check is treated as corrupt and rejected (tier 1).
        /// </summary>
        public bool IsPhysicallyPlausible(double value)
            => !double.IsNaN(value)
               && !double.IsInfinity(value)
               && value >= PhysicalMin
               && value <= PhysicalMax;

        /// <summary>True when the value is inside the operating specification band (tier 2).</summary>
        public bool IsWithinOperatingSpec(double value)
            => value >= OperatingMin && value <= OperatingMax;

        /// <summary>
        /// Classifies a value into <see cref="SensorValidationState"/> using the two-tier policy:
        /// implausible → Invalid; plausible but out of spec → OutOfSpec; otherwise Normal.
        /// </summary>
        public SensorValidationState Classify(double value)
        {
            if (!IsPhysicallyPlausible(value))
                return SensorValidationState.Invalid;

            return IsWithinOperatingSpec(value)
                ? SensorValidationState.Normal
                : SensorValidationState.OutOfSpec;
        }
    }

    /// <summary>
    /// Maps the fixed SENSOR_ID convention (OAI-003) to a <see cref="SensorParameter"/> definition.
    /// 0x01 = conductivity, 0x02 = temperature, 0x03 = pH, 0x04 = pressure/flow.
    /// Sensor IDs are matched in the same formatted form the parser produces (e.g. "0x01").
    ///
    /// The range bounds are user-configurable (RC-008 / SRS-C-003): call <see cref="Configure"/>
    /// at startup with overrides loaded from a configuration file. Any bound not supplied falls
    /// back to the built-in default below, and an override that would violate range consistency
    /// is rejected (logged) in favour of the default. NOTE: integrity protection of that
    /// configuration is RC-011 (config protection), which is still pending.
    /// </summary>
    public static class SensorParameterRegistry
    {
        private static readonly IReadOnlyDictionary<string, SensorParameter> Defaults =
            new Dictionary<string, SensorParameter>(StringComparer.OrdinalIgnoreCase)
            {
                ["0x01"] = new SensorParameter("Conductivity", "µS/cm", operatingMin: 0.0, operatingMax: 1.3, physicalMin: 0.0, physicalMax: 200.0),
                ["0x02"] = new SensorParameter("Temperature", "°C", operatingMin: 15.0, operatingMax: 30.0, physicalMin: -10.0, physicalMax: 130.0),
                ["0x03"] = new SensorParameter("pH", "pH", operatingMin: 5.0, operatingMax: 7.0, physicalMin: 0.0, physicalMax: 14.0),
                ["0x04"] = new SensorParameter("Pressure", "bar", operatingMin: 1.0, operatingMax: 6.0, physicalMin: 0.0, physicalMax: 16.0),
            };

        private static readonly object Gate = new();
        private static Dictionary<string, SensorParameter> _active = CopyOf(Defaults);

        /// <summary>The sensor ids covered by the fixed convention (e.g. "0x01"…"0x04").</summary>
        public static IReadOnlyCollection<string> KnownSensorIds => (IReadOnlyCollection<string>)Defaults.Keys;

        /// <summary>
        /// Returns the active parameter definition for a sensor id, or null when the id is not in
        /// the known convention (an unknown sensor is displayed without range validation).
        /// </summary>
        public static SensorParameter? ForSensorId(string sensorId)
        {
            if (sensorId == null)
                return null;

            var snapshot = _active;
            return snapshot.TryGetValue(sensorId, out var def) ? def : null;
        }

        /// <summary>Returns the built-in default definition for a sensor id (ignores overrides).</summary>
        public static SensorParameter? DefaultForSensorId(string sensorId)
            => sensorId != null && Defaults.TryGetValue(sensorId, out var def) ? def : null;

        /// <summary>
        /// Applies user-supplied range overrides on top of the built-in defaults (RC-008 / SRS-C-003).
        /// Each known sensor id's bounds are taken from the override where provided and from the
        /// default otherwise; the merged result must satisfy
        /// physicalMin ≤ operatingMin ≤ operatingMax ≤ physicalMax with all bounds finite, else the
        /// override for that sensor is rejected (logged) and the default is kept. Overrides for
        /// unknown sensor ids are ignored (logged). Passing null resets every parameter to defaults.
        /// </summary>
        public static void Configure(IReadOnlyDictionary<string, SensorRangeOverride>? overrides)
        {
            lock (Gate)
            {
                var effective = CopyOf(Defaults);

                if (overrides != null)
                {
                    foreach (var (sensorId, ovr) in overrides)
                    {
                        if (ovr == null)
                            continue;

                        if (!Defaults.TryGetValue(sensorId, out var def))
                        {
                            Log.Warning(
                                "Ignoring sensor range override for unknown sensor id {SensorId}; known ids are {KnownIds}",
                                sensorId, string.Join(", ", Defaults.Keys));
                            continue;
                        }

                        var merged = Merge(def, ovr);
                        if (TryValidate(merged, out var reason))
                        {
                            effective[sensorId] = merged;
                            Log.Information(
                                "Applied sensor range override for {SensorId} ({Name}): operating {OpMin}..{OpMax}, physical {PhMin}..{PhMax}",
                                sensorId, def.Name, merged.OperatingMin, merged.OperatingMax, merged.PhysicalMin, merged.PhysicalMax);
                        }
                        else
                        {
                            Log.Error(
                                "Rejected sensor range override for {SensorId} ({Name}): {Reason}; keeping default operating {OpMin}..{OpMax}, physical {PhMin}..{PhMax}",
                                sensorId, def.Name, reason, def.OperatingMin, def.OperatingMax, def.PhysicalMin, def.PhysicalMax);
                        }
                    }
                }

                _active = effective;
            }
        }

        /// <summary>Resets every parameter to its built-in default. Equivalent to <c>Configure(null)</c>.</summary>
        public static void ResetToDefaults() => Configure(null);

        private static SensorParameter Merge(SensorParameter def, SensorRangeOverride ovr)
            => new SensorParameter(
                def.Name,
                def.Unit,
                operatingMin: ovr.OperatingMin ?? def.OperatingMin,
                operatingMax: ovr.OperatingMax ?? def.OperatingMax,
                physicalMin: ovr.PhysicalMin ?? def.PhysicalMin,
                physicalMax: ovr.PhysicalMax ?? def.PhysicalMax);

        private static bool TryValidate(SensorParameter p, out string reason)
        {
            double[] bounds = { p.OperatingMin, p.OperatingMax, p.PhysicalMin, p.PhysicalMax };
            if (bounds.Any(b => double.IsNaN(b) || double.IsInfinity(b)))
            {
                reason = "bounds must be finite numbers";
                return false;
            }

            if (p.PhysicalMin > p.OperatingMin || p.OperatingMin > p.OperatingMax || p.OperatingMax > p.PhysicalMax)
            {
                reason = $"bounds must satisfy physicalMin ≤ operatingMin ≤ operatingMax ≤ physicalMax " +
                         $"(got physical {p.PhysicalMin}..{p.PhysicalMax}, operating {p.OperatingMin}..{p.OperatingMax})";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static Dictionary<string, SensorParameter> CopyOf(IReadOnlyDictionary<string, SensorParameter> source)
            => new(source, StringComparer.OrdinalIgnoreCase);
    }
}
