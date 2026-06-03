namespace WaterFilterCBZ.Models
{
    /// <summary>
    /// Represents a single sensor reading with timestamp.
    /// </summary>
    public class SensorSample
    {
        public string SensorId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }

        public override string ToString() => $"{SensorId}: {Value:F2} @ {Timestamp:HH:mm:ss}";
    }
}
