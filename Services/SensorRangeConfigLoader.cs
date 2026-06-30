using System.IO;
using System.Text.Json;
using WaterFilterCBZ.Models;
using Serilog;

namespace WaterFilterCBZ.Services
{
    /// <summary>
    /// Loads user-supplied sensor range overrides (RC-008 / SRS-C-003) from a JSON file.
    /// The file maps sensor ids to partial range overrides, e.g.:
    /// <code>
    /// {
    ///   "0x01": { "operatingMax": 1.1, "physicalMax": 250.0 },
    ///   "0x03": { "operatingMin": 6.0, "operatingMax": 8.0 }
    /// }
    /// </code>
    /// Any omitted bound keeps the built-in default. A missing file means "use defaults" and is
    /// not an error; a malformed file is logged and treated as "no overrides" so a bad edit can
    /// never silently widen the safety thresholds.
    /// </summary>
    public static class SensorRangeConfigLoader
    {
        /// <summary>Environment variable that overrides the default configuration file path.</summary>
        public const string PathEnvironmentVariable = "WATERFILTERCBZ_SENSOR_RANGES";

        private const string DefaultFileName = "sensor-ranges.json";

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Default configuration file path: <c>%AppData%\WaterFilterCBZ\sensor-ranges.json</c>,
        /// unless overridden by the <see cref="PathEnvironmentVariable"/> environment variable.
        /// </summary>
        public static string DefaultConfigPath
        {
            get
            {
                var fromEnv = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(fromEnv))
                    return fromEnv;

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WaterFilterCBZ",
                    DefaultFileName);
            }
        }

        /// <summary>Loads overrides from <see cref="DefaultConfigPath"/>.</summary>
        public static IReadOnlyDictionary<string, SensorRangeOverride> Load()
            => LoadFromFile(DefaultConfigPath);

        /// <summary>
        /// Loads overrides from the given path. Returns an empty map (never null) when the file is
        /// absent, empty, or cannot be parsed; the reason is logged. The returned overrides are not
        /// validated here — <see cref="SensorParameterRegistry.Configure"/> performs consistency checks.
        /// </summary>
        public static IReadOnlyDictionary<string, SensorRangeOverride> LoadFromFile(string path)
        {
            var empty = new Dictionary<string, SensorRangeOverride>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Log.Information("No sensor range configuration at {Path}; using built-in defaults", path);
                return empty;
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Information("Sensor range configuration {Path} is empty; using built-in defaults", path);
                    return empty;
                }

                var parsed = JsonSerializer.Deserialize<Dictionary<string, SensorRangeOverride>>(json, Options);
                if (parsed == null || parsed.Count == 0)
                {
                    Log.Information("Sensor range configuration {Path} contained no overrides; using built-in defaults", path);
                    return empty;
                }

                Log.Information("Loaded {Count} sensor range override(s) from {Path}", parsed.Count, path);
                return new Dictionary<string, SensorRangeOverride>(parsed, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read sensor range configuration {Path}; using built-in defaults", path);
                return empty;
            }
        }
    }
}
