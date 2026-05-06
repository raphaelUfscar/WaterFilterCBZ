using WaterFilterCBZ.Services;

namespace WaterFilterCBZ.Tests;

public class SerialPortServiceTests
{
    [Fact]
    public void Constructor_WithNullSampleCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SerialPortService("COM1", null!));
    }

    [Fact]
    public void NewService_IsDisconnected()
    {
        using ISerialPortService service = CreateService();

        Assert.False(service.IsConnected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SetPort_WithBlankPortName_Throws(string portName)
    {
        using ISerialPortService service = CreateService();

        Assert.Throws<ArgumentException>(() => service.SetPort(portName));
    }

    [Fact]
    public void SetPort_WhenDisconnected_AcceptsValidPortName()
    {
        using ISerialPortService service = CreateService();

        service.SetPort("COM2");

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Disconnect_WhenDisconnected_DoesNotRaiseConnectionStatusChanged()
    {
        using ISerialPortService service = CreateService();
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Disconnect();

        Assert.Equal(0, eventCount);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Connect_WhenPortCannotBeOpened_RaisesConnectionStatusChangedAndStaysDisconnected()
    {
        using ISerialPortService service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Connect();

        Assert.Equal(1, eventCount);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        var service = CreateService();

        service.Dispose();
        service.Dispose();

        Assert.False(service.IsConnected);
    }

    private static SerialPortService CreateService(string portName = "COM1")
    {
        return new SerialPortService(portName, _ => { });
    }
}
