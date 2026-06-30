namespace WaterFilterCBZ.Models
{
    /// <summary>
    /// User-supplied override of a sensor parameter's range bounds (RC-008 / SRS-C-003).
    /// Every field is optional: a null field keeps the built-in default for that bound, so a
    /// configuration may tune only the values it cares about. Cross-field consistency of the
    /// merged result is validated by <see cref="SensorParameterRegistry.Configure"/>.
    /// </summary>
    public sealed class SensorRangeOverride
    {
        /// <summary>Lower bound of the operating specification (inclusive), or null to keep the default.</summary>
        public double? OperatingMin { get; set; }

        /// <summary>Upper bound of the operating specification (inclusive), or null to keep the default.</summary>
        public double? OperatingMax { get; set; }

        /// <summary>Lower bound of the physically plausible range (inclusive), or null to keep the default.</summary>
        public double? PhysicalMin { get; set; }

        /// <summary>Upper bound of the physically plausible range (inclusive), or null to keep the default.</summary>
        public double? PhysicalMax { get; set; }
    }
}
