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
    /// </summary>
    public static class SensorParameterRegistry
    {
        private static readonly Dictionary<string, SensorParameter> Definitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["0x01"] = new SensorParameter("Conductivity", "µS/cm", operatingMin: 0.0, operatingMax: 1.3, physicalMin: 0.0, physicalMax: 200.0),
            ["0x02"] = new SensorParameter("Temperature", "°C", operatingMin: 15.0, operatingMax: 30.0, physicalMin: -10.0, physicalMax: 130.0),
            ["0x03"] = new SensorParameter("pH", "pH", operatingMin: 5.0, operatingMax: 7.0, physicalMin: 0.0, physicalMax: 14.0),
            ["0x04"] = new SensorParameter("Pressure", "bar", operatingMin: 1.0, operatingMax: 6.0, physicalMin: 0.0, physicalMax: 16.0),
        };

        /// <summary>
        /// Returns the parameter definition for a sensor id, or null when the id is not in the
        /// known convention (an unknown sensor is displayed without range validation).
        /// </summary>
        public static SensorParameter? ForSensorId(string sensorId)
            => sensorId != null && Definitions.TryGetValue(sensorId, out var def) ? def : null;
    }
}
