using WaterFilterCBZ.Services;
using WaterFilterCBZ.Models;
using System.Reflection;

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
        using var service = CreateService();

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void NewService_UsesDefaultBaudRate()
    {
        using var service = CreateService();

        Assert.Equal(115200, service.BaudRate);
    }

    [Fact]
    public void BaudRate_CanBeChanged()
    {
        using var service = CreateService();

        service.BaudRate = 9600;

        Assert.Equal(9600, service.BaudRate);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SetPort_WithBlankPortName_Throws(string portName)
    {
        using var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.SetPort(portName));
    }

    [Fact]
    public void SetPort_WhenDisconnected_AcceptsValidPortName()
    {
        using var service = CreateService();

        service.SetPort("COM2");

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Disconnect_WhenDisconnected_DoesNotRaiseConnectionStatusChanged()
    {
        using var service = CreateService();
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Disconnect();

        Assert.Equal(0, eventCount);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Connect_WhenPortCannotBeOpened_RaisesConnectionStatusChangedAndStaysDisconnected()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
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

    [Fact]
    public void ParseFrame_DecodesMcuTimestampsRelativeToFirstSample()
    {
        var samples = new List<SensorSample>();
        using var service = new SerialPortService("COM1", samples.Add);
        var frame = CreateFrame(
            CreateSensorEntry(sensorId: 0x01, timestampMs: 10_000, unitId: 0x01, value: 12.5f),
            CreateSensorEntry(sensorId: 0x02, timestampMs: 11_500, unitId: 0x01, value: 7.25f));

        InvokeParseFrame(service, frame);

        Assert.Collection(
            samples,
            first =>
            {
                Assert.Equal("0x01", first.SensorId);
                Assert.Equal(12.5f, first.Value);
            },
            second =>
            {
                Assert.Equal("0x02", second.SensorId);
                Assert.Equal(7.25f, second.Value);
                Assert.Equal(samples[0].Timestamp.AddMilliseconds(1_500), second.Timestamp);
            });
    }

    private static SerialPortService CreateService(string portName = "COM1")
    {
        return new SerialPortService(portName, _ => { });
    }

    private static byte[] CreateFrame(params byte[][] entries)
    {
        var frame = new List<byte> { 0xAA, (byte)entries.Length };
        foreach (var entry in entries)
        {
            frame.AddRange(entry);
        }

        frame.Add(0x00);
        frame.Add(0x55);

        return frame.ToArray();
    }

    private static byte[] CreateSensorEntry(byte sensorId, uint timestampMs, byte unitId, float value)
    {
        var entry = new List<byte> { sensorId };
        entry.AddRange(BitConverter.GetBytes(timestampMs));
        entry.Add(unitId);
        entry.AddRange(BitConverter.GetBytes(value));

        return entry.ToArray();
    }

    private static void InvokeParseFrame(SerialPortService service, byte[] frame)
    {
        var parseFrame = typeof(SerialPortService).GetMethod(
            "ParseFrame",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(parseFrame);
        parseFrame.Invoke(service, [frame]);
    }
}
