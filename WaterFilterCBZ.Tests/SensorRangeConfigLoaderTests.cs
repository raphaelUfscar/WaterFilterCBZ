using System.IO;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.Services;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises loading user-supplied sensor range overrides from JSON (RC-008 / SRS-C-003):
/// a missing or malformed file yields no overrides (defaults stand); a valid file is parsed
/// with omitted bounds left null for per-field fallback in the registry.
/// </summary>
public sealed class SensorRangeConfigLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { /* best effort */ }
        }
    }

    private string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sensor-ranges-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void LoadFromFile_MissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Empty(result);
    }

    [Fact]
    public void LoadFromFile_MalformedJson_ReturnsEmpty()
    {
        var path = WriteTemp("{ this is not valid json ");

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Empty(result);
    }

    [Fact]
    public void LoadFromFile_EmptyFile_ReturnsEmpty()
    {
        var path = WriteTemp("   ");

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Empty(result);
    }

    [Fact]
    public void LoadFromFile_ValidFile_ParsesOverridesWithPartialFields()
    {
        var path = WriteTemp("""
            {
              "0x01": { "operatingMax": 1.1, "physicalMax": 250.0 },
              "0x03": { "operatingMin": 6.0, "operatingMax": 8.0 }
            }
            """);

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Equal(2, result.Count);

        var conductivity = result["0x01"];
        Assert.Equal(1.1, conductivity.OperatingMax);
        Assert.Equal(250.0, conductivity.PhysicalMax);
        Assert.Null(conductivity.OperatingMin); // omitted -> falls back to default in the registry
        Assert.Null(conductivity.PhysicalMin);

        var ph = result["0x03"];
        Assert.Equal(6.0, ph.OperatingMin);
        Assert.Equal(8.0, ph.OperatingMax);
    }

    [Fact]
    public void LoadFromFile_SkipsJsonComments()
    {
        // Mirrors the documented docs/sensor-ranges.example.json format (JSONC).
        var path = WriteTemp("""
            // leading comment
            {
              "0x01": { "operatingMax": 1.2 } // trailing comment
            }
            """);

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Equal(1.2, result["0x01"].OperatingMax);
    }

    [Fact]
    public void LoadFromFile_IsCaseInsensitiveOnPropertyNames()
    {
        var path = WriteTemp("""{ "0x02": { "OperatingMin": 16.0, "PHYSICALMAX": 140.0 } }""");

        var result = SensorRangeConfigLoader.LoadFromFile(path);

        Assert.Equal(16.0, result["0x02"].OperatingMin);
        Assert.Equal(140.0, result["0x02"].PhysicalMax);
    }

    [Fact]
    public void LoadedOverrides_FeedRegistry_EndToEnd()
    {
        var path = WriteTemp("""{ "0x01": { "operatingMax": 1.1 } }""");

        try
        {
            SensorParameterRegistry.Configure(SensorRangeConfigLoader.LoadFromFile(path));

            Assert.Equal(1.1, SensorParameterRegistry.ForSensorId("0x01")!.OperatingMax);
        }
        finally
        {
            SensorParameterRegistry.ResetToDefaults();
        }
    }
}
