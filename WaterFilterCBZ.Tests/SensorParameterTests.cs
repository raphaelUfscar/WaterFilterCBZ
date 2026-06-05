using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

public class SensorParameterTests
{
    [Theory]
    [InlineData("0x01", "Conductivity", "µS/cm")]
    [InlineData("0x02", "Temperature", "°C")]
    [InlineData("0x03", "pH", "pH")]
    [InlineData("0x04", "Pressure", "bar")]
    public void Registry_MapsKnownSensorIds_ToParameters(string sensorId, string name, string unit)
    {
        var param = SensorParameterRegistry.ForSensorId(sensorId);

        Assert.NotNull(param);
        Assert.Equal(name, param!.Name);
        Assert.Equal(unit, param.Unit);
    }

    [Theory]
    [InlineData("0x05")]
    [InlineData("0x00")]
    [InlineData("garbage")]
    public void Registry_ReturnsNull_ForUnknownSensorId(string sensorId)
    {
        Assert.Null(SensorParameterRegistry.ForSensorId(sensorId));
    }

    [Fact]
    public void Classify_ReturnsNormal_WithinOperatingSpec()
    {
        var ph = SensorParameterRegistry.ForSensorId("0x03")!; // operating 5.0–7.0
        Assert.Equal(SensorValidationState.Normal, ph.Classify(6.5));
    }

    [Fact]
    public void Classify_ReturnsOutOfSpec_PlausibleButOutsideSpec()
    {
        var ph = SensorParameterRegistry.ForSensorId("0x03")!; // operating 5.0–7.0, physical 0–14
        Assert.Equal(SensorValidationState.OutOfSpec, ph.Classify(9.0));
    }

    [Fact]
    public void Classify_ReturnsInvalid_OutsidePhysicalRange()
    {
        var ph = SensorParameterRegistry.ForSensorId("0x03")!; // physical 0–14
        Assert.Equal(SensorValidationState.Invalid, ph.Classify(99.0));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Classify_ReturnsInvalid_ForNonFiniteValues(double value)
    {
        var temp = SensorParameterRegistry.ForSensorId("0x02")!;
        Assert.Equal(SensorValidationState.Invalid, temp.Classify(value));
    }

    [Fact]
    public void IsWithinOperatingSpec_IsInclusiveOfBounds()
    {
        var conductivity = SensorParameterRegistry.ForSensorId("0x01")!; // 0.0–1.3
        Assert.True(conductivity.IsWithinOperatingSpec(0.0));
        Assert.True(conductivity.IsWithinOperatingSpec(1.3));
        Assert.False(conductivity.IsWithinOperatingSpec(1.31));
    }
}
