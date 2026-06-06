using WaterFilterCBZ.Utils;

namespace WaterFilterCBZ.Tests;

public class SerialPortHelperTests
{
    [Fact]
    public void GetAvailablePorts_ReturnsNonNullArray()
    {
        var ports = SerialPortHelper.GetAvailablePorts();

        Assert.NotNull(ports);
    }

    [Fact]
    public void IsPortAvailable_UnknownPort_ReturnsFalse()
    {
        Assert.False(SerialPortHelper.IsPortAvailable("COM_DOES_NOT_EXIST_9999"));
    }

    [Fact]
    public void IsPortAvailable_ReportsEveryEnumeratedPortAsAvailable()
    {
        var ports = SerialPortHelper.GetAvailablePorts();

        // Whatever ports the machine reports must themselves be considered available
        // (case-insensitively). On an agent with no ports this asserts nothing, which is fine.
        Assert.All(ports, p => Assert.True(SerialPortHelper.IsPortAvailable(p.ToLowerInvariant())));
    }
}
