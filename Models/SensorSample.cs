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
        
        /// <summary>
        /// Parse a CSV-formatted sensor line: "sensorId,timestamp,value"
        /// where timestamp is Unix seconds.
        /// </summary>
        public static bool TryParseCsv(string line, out SensorSample? sample)
        {
            sample = null;
            try
            {
                var parts = line.Trim().Split(',');
                if (parts.Length != 3)
                    return false;

                var sensorId = parts[0].Trim();
                if (!long.TryParse(parts[1].Trim(), out var unixSeconds))
                    return false;

                if (!double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                    return false;

                var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;

                sample = new SensorSample
                {
                    SensorId = sensorId,
                    Timestamp = timestamp,
                    Value = value
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override string ToString() => $"{SensorId}: {Value:F2} @ {Timestamp:HH:mm:ss}";
    }
}
