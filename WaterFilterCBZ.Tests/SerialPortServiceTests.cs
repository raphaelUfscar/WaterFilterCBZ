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
    public void Constructor_WithNullConnectionFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SerialPortService("COM1", _ => { }, null!));
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
    public void NewService_EnablesAutoReconnectWithDefaultHeartbeatSettings()
    {
        using var service = CreateService();

        Assert.True(service.AutoReconnectEnabled);
        Assert.Equal(TimeSpan.FromSeconds(2), service.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(10), service.HeartbeatTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), service.ReconnectDelay);
    }

    [Fact]
    public void BaudRate_CanBeChanged()
    {
        using var service = CreateService();

        service.BaudRate = 9600;

        Assert.Equal(9600, service.BaudRate);
    }

    [Fact]
    public void HeartbeatSettings_RejectInvalidValues()
    {
        using var service = CreateService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.HeartbeatInterval = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.HeartbeatTimeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.ReconnectDelay = TimeSpan.FromMilliseconds(-1));
    }

    [Fact]
    public void HeartbeatSettings_AcceptValidValues()
    {
        using var service = CreateService();

        service.HeartbeatInterval = TimeSpan.FromMilliseconds(250);
        service.HeartbeatTimeout = TimeSpan.FromSeconds(5);
        service.ReconnectDelay = TimeSpan.Zero;
        service.AutoReconnectEnabled = false;

        Assert.Equal(TimeSpan.FromMilliseconds(250), service.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), service.HeartbeatTimeout);
        Assert.Equal(TimeSpan.Zero, service.ReconnectDelay);
        Assert.False(service.AutoReconnectEnabled);
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
    public void SetPort_WhenConnected_DisconnectsCurrentPort()
    {
        var firstPort = new FakeSerialConnection();
        using var service = CreateService(firstPort);

        service.Connect();
        service.SetPort("COM2");

        Assert.False(firstPort.IsOpen);
        Assert.True(firstPort.IsDisposed);
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
    public void Connect_WhenPortOpens_StartsConnectedLifecycle()
    {
        var port = new FakeSerialConnection();
        using var service = CreateService(port);
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Connect();

        Assert.True(service.IsConnected);
        Assert.True(port.IsOpen);
        Assert.Equal(1, port.OpenCount);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Connect_WhenAlreadyConnected_DoesNotOpenAnotherPort()
    {
        var factoryCalls = 0;
        using var service = new SerialPortService(
            "COM1",
            _ => { },
            (_, _) =>
            {
                factoryCalls++;
                return new FakeSerialConnection();
            });

        service.Connect();
        service.Connect();

        Assert.Equal(1, factoryCalls);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public void Disconnect_WhenConnected_ClosesPortAndRaisesStatusChanged()
    {
        var port = new FakeSerialConnection();
        using var service = CreateService(port);
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Connect();
        service.Disconnect();

        Assert.False(service.IsConnected);
        Assert.False(port.IsOpen);
        Assert.True(port.IsDisposed);
        Assert.Equal(1, port.CloseCount);
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Connect_WhenFactoryThrows_RaisesConnectionStatusChangedAndStaysDisconnected()
    {
        using var service = new SerialPortService(
            "COM1",
            _ => { },
            (_, _) => throw new InvalidOperationException("factory failed"));
        var eventCount = 0;
        service.ConnectionStatusChanged += (_, _) => eventCount++;

        service.Connect();

        Assert.Equal(1, eventCount);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public void Connect_WhenOpenThrows_DisposesCreatedPort()
    {
        var port = new FakeSerialConnection { ThrowOnOpen = true };
        using var service = CreateService(port);

        service.Connect();

        Assert.False(service.IsConnected);
        Assert.True(port.IsDisposed);
    }

    [Fact]
    public async Task DataReceived_WhenCompleteFrameArrives_DispatchesSample()
    {
        var samples = new List<SensorSample>();
        var port = new FakeSerialConnection();
        using var service = new SerialPortService("COM1", samples.Add, (_, _) => port);
        var frame = CreateFrame(CreateSensorEntry(sensorId: 0x01, timestampMs: 10_000, unitId: 0x01, value: 12.5f));

        service.Connect();
        port.EnqueueRead(frame);
        port.RaiseDataReceived();

        await WaitUntilAsync(() => samples.Count == 1);
        Assert.Equal("0x01", samples[0].SensorId);
        Assert.Equal(12.5f, samples[0].Value);
    }

    [Fact]
    public async Task DataReceived_WhenReadReturnsFewerBytes_ResizesChunkBeforeProcessing()
    {
        var samples = new List<SensorSample>();
        var port = new FakeSerialConnection { ReportedBytesToRead = 64 };
        using var service = new SerialPortService("COM1", samples.Add, (_, _) => port);
        var frame = CreateFrame(CreateSensorEntry(sensorId: 0x02, timestampMs: 20_000, unitId: 0x01, value: 9.5f));

        service.Connect();
        port.EnqueueRead(frame);
        port.RaiseDataReceived();

        await WaitUntilAsync(() => samples.Count == 1);
        Assert.Equal("0x02", samples[0].SensorId);
        Assert.Equal(9.5f, samples[0].Value);
    }

    [Fact]
    public void DataReceived_WhenNoBytesAreAvailable_DoesNothing()
    {
        var port = new FakeSerialConnection();
        using var service = CreateService(port);

        service.Connect();
        port.RaiseDataReceived();

        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task DataReceived_WhenReadFails_SchedulesReconnect()
    {
        var ports = new Queue<FakeSerialConnection>([
            new FakeSerialConnection { ThrowOnRead = true },
            new FakeSerialConnection()
        ]);
        using var service = CreateServiceFromQueue(ports);
        service.ReconnectDelay = TimeSpan.FromMilliseconds(1);

        service.Connect();
        var firstPort = GetPrivateField<ISerialConnection>(service, "_port") as FakeSerialConnection;
        Assert.NotNull(firstPort);
        firstPort.EnqueueRead([0xAA]);
        firstPort.RaiseDataReceived();

        await WaitUntilAsync(() => service.IsConnected && !ReferenceEquals(firstPort, GetPrivateField<ISerialConnection>(service, "_port")));
        Assert.True(firstPort.IsDisposed);
    }

    [Fact]
    public async Task ErrorReceived_WhenPortReportsError_SchedulesReconnect()
    {
        var ports = new Queue<FakeSerialConnection>([
            new FakeSerialConnection(),
            new FakeSerialConnection()
        ]);
        using var service = CreateServiceFromQueue(ports);
        service.ReconnectDelay = TimeSpan.FromMilliseconds(1);

        service.Connect();
        var firstPort = GetPrivateField<ISerialConnection>(service, "_port") as FakeSerialConnection;
        Assert.NotNull(firstPort);
        firstPort.RaiseErrorReceived("Frame");

        await WaitUntilAsync(() => service.IsConnected && !ReferenceEquals(firstPort, GetPrivateField<ISerialConnection>(service, "_port")));
        Assert.True(firstPort.IsDisposed);
    }

    [Fact]
    public async Task HeartbeatTimeout_WhenConnectedPortIsIdle_SchedulesReconnect()
    {
        var ports = new Queue<FakeSerialConnection>([
            new FakeSerialConnection(),
            new FakeSerialConnection()
        ]);
        using var service = CreateServiceFromQueue(ports);
        service.HeartbeatInterval = TimeSpan.FromMilliseconds(1);
        service.HeartbeatTimeout = TimeSpan.FromMilliseconds(1);
        service.ReconnectDelay = TimeSpan.FromMilliseconds(1);

        service.Connect();
        var firstPort = GetPrivateField<ISerialConnection>(service, "_port") as FakeSerialConnection;
        Assert.NotNull(firstPort);

        await WaitUntilAsync(() => service.IsConnected && !ReferenceEquals(firstPort, GetPrivateField<ISerialConnection>(service, "_port")));
        Assert.True(firstPort.IsDisposed);
    }

    [Fact]
    public void ScheduleReconnect_WhenAutoReconnectDisabled_DoesNotStartReconnect()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        service.AutoReconnectEnabled = false;
        SetPrivateField(service, "_manualDisconnectRequested", false);

        InvokeScheduleReconnect(service, "test");

        Assert.Equal(0, GetPrivateField<int>(service, "_reconnectInProgress"));
    }

    [Fact]
    public void ScheduleReconnect_WhenManualDisconnectRequested_DoesNotStartReconnect()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");

        InvokeScheduleReconnect(service, "test");

        Assert.Equal(0, GetPrivateField<int>(service, "_reconnectInProgress"));
    }

    [Fact]
    public void ScheduleReconnect_WhenDisposed_DoesNotStartReconnect()
    {
        var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        service.Dispose();
        SetPrivateField(service, "_manualDisconnectRequested", false);

        InvokeScheduleReconnect(service, "test");

        Assert.Equal(0, GetPrivateField<int>(service, "_reconnectInProgress"));
    }

    [Fact]
    public void ScheduleReconnect_WhenReconnectAlreadyInProgress_DoesNotStartAnotherReconnect()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        SetPrivateField(service, "_manualDisconnectRequested", false);
        SetPrivateField(service, "_reconnectInProgress", 1);

        InvokeScheduleReconnect(service, "test");

        Assert.Equal(1, GetPrivateField<int>(service, "_reconnectInProgress"));
    }

    [Fact]
    public async Task ScheduleReconnect_WhenEnabled_RetriesUntilManualDisconnectIsRequested()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        service.ReconnectDelay = TimeSpan.FromMilliseconds(1);
        SetPrivateField(service, "_manualDisconnectRequested", false);

        InvokeScheduleReconnect(service, "test");
        await WaitUntilAsync(() => GetPrivateField<int>(service, "_reconnectInProgress") == 1);

        SetPrivateField(service, "_manualDisconnectRequested", true);

        await WaitUntilAsync(() => GetPrivateField<int>(service, "_reconnectInProgress") == 0);
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ReconnectAsync_WhenManualDisconnectAlreadyRequested_ResetsReconnectFlag()
    {
        using var service = CreateService("COM_DOES_NOT_EXIST_FOR_TESTS");
        SetPrivateField(service, "_reconnectInProgress", 1);

        await InvokeReconnectAsync(service, "test");

        Assert.Equal(0, GetPrivateField<int>(service, "_reconnectInProgress"));
        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ReconnectAsync_WhenNextPortOpens_ReplacesConnection()
    {
        var firstPort = new FakeSerialConnection();
        var secondPort = new FakeSerialConnection();
        var ports = new Queue<FakeSerialConnection>([firstPort, secondPort]);
        using var service = CreateServiceFromQueue(ports);
        service.ReconnectDelay = TimeSpan.Zero;

        service.Connect();
        SetPrivateField(service, "_manualDisconnectRequested", false);
        await InvokeReconnectAsync(service, "test");

        Assert.False(firstPort.IsOpen);
        Assert.True(firstPort.IsDisposed);
        Assert.True(secondPort.IsOpen);
        Assert.True(service.IsConnected);
    }

    [Fact]
    public async Task MonitorHeartbeatAsync_WhenCanceled_ExitsWithoutChangingConnectionState()
    {
        using var service = CreateService();
        using var cts = new CancellationTokenSource();
        service.HeartbeatInterval = TimeSpan.FromMilliseconds(50);

        var monitorTask = InvokeMonitorHeartbeatAsync(service, cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(5));

        await monitorTask;

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void ClosePort_WhenBackgroundTaskWasCanceled_DoesNotThrow()
    {
        using var service = CreateService();
        SetPrivateField<Task?>(service, "_readTask", Task.FromCanceled(new CancellationToken(canceled: true)));

        InvokeClosePort(service, raiseStatusChanged: false);

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void ClosePort_WhenPortCloseThrows_HandlesException()
    {
        var port = new FakeSerialConnection { ThrowOnClose = true };
        using var service = CreateService(port);

        service.Connect();
        InvokeClosePort(service, raiseStatusChanged: true);

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void ClosePort_WhenPortIsAlreadyClosed_DisposesWithoutClosingAgain()
    {
        var port = new FakeSerialConnection();
        using var service = CreateService();
        SetPrivateField<ISerialConnection?>(service, "_port", port);

        InvokeClosePort(service, raiseStatusChanged: true);

        Assert.Equal(0, port.CloseCount);
        Assert.True(port.IsDisposed);
    }

    [Fact]
    public void DataReceived_WhenSenderIsNotSerialConnection_DoesNothing()
    {
        using var service = CreateService();

        InvokeOnDataReceived(service, new object());

        Assert.False(service.IsConnected);
    }

    [Fact]
    public void DataReceived_WhenPortIsClosed_DoesNothing()
    {
        var port = new FakeSerialConnection();
        using var service = CreateService();

        InvokeOnDataReceived(service, port);

        Assert.False(service.IsConnected);
    }

    [Fact]
    public async Task ProcessIncomingDataAsync_WhenCanceled_ExitsCleanly()
    {
        using var service = CreateService();
        using var cts = new CancellationTokenSource();
        var task = InvokeProcessIncomingDataAsync(service, cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(5));

        await task;
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

    private static SerialPortService CreateService(FakeSerialConnection port)
    {
        return new SerialPortService("COM1", _ => { }, (_, _) => port);
    }

    private static SerialPortService CreateServiceFromQueue(Queue<FakeSerialConnection> ports)
    {
        return new SerialPortService("COM1", _ => { }, (_, _) => ports.Dequeue());
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

    private static void InvokeScheduleReconnect(SerialPortService service, string reason)
    {
        var scheduleReconnect = typeof(SerialPortService).GetMethod(
            "ScheduleReconnect",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(scheduleReconnect);
        scheduleReconnect.Invoke(service, [reason]);
    }

    private static Task InvokeReconnectAsync(SerialPortService service, string reason)
    {
        var reconnectAsync = typeof(SerialPortService).GetMethod(
            "ReconnectAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(reconnectAsync);
        return (Task)reconnectAsync.Invoke(service, [reason])!;
    }

    private static Task InvokeMonitorHeartbeatAsync(SerialPortService service, CancellationToken cancellationToken)
    {
        var monitorHeartbeatAsync = typeof(SerialPortService).GetMethod(
            "MonitorHeartbeatAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(monitorHeartbeatAsync);
        return (Task)monitorHeartbeatAsync.Invoke(service, [cancellationToken])!;
    }

    private static void InvokeClosePort(SerialPortService service, bool raiseStatusChanged)
    {
        var closePort = typeof(SerialPortService).GetMethod(
            "ClosePort",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(closePort);
        closePort.Invoke(service, [raiseStatusChanged]);
    }

    private static void InvokeOnDataReceived(SerialPortService service, object sender)
    {
        var onDataReceived = typeof(SerialPortService).GetMethod(
            "OnDataReceived",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(onDataReceived);
        onDataReceived.Invoke(service, [sender, EventArgs.Empty]);
    }

    private static Task InvokeProcessIncomingDataAsync(SerialPortService service, CancellationToken cancellationToken)
    {
        var processIncomingDataAsync = typeof(SerialPortService).GetMethod(
            "ProcessIncomingDataAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(processIncomingDataAsync);
        return (Task)processIncomingDataAsync.Invoke(service, [cancellationToken])!;
    }

    private static T GetPrivateField<T>(SerialPortService service, string fieldName)
    {
        var field = typeof(SerialPortService).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return (T)field.GetValue(service)!;
    }

    private static void SetPrivateField<T>(SerialPortService service, string fieldName, T value)
    {
        var field = typeof(SerialPortService).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field.SetValue(service, value);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private sealed class FakeSerialConnection : ISerialConnection
    {
        private readonly Queue<byte[]> _reads = new();

        public bool ThrowOnOpen { get; set; }
        public bool ThrowOnRead { get; set; }
        public bool ThrowOnClose { get; set; }
        public int? ReportedBytesToRead { get; set; }
        public bool IsOpen { get; private set; }
        public bool IsDisposed { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int BytesToRead => ReportedBytesToRead ?? (_reads.TryPeek(out var read) ? read.Length : 0);
        public event EventHandler<EventArgs>? DataReceived;
        public event EventHandler<SerialPortErrorEventArgs>? ErrorReceived;

        public void Open()
        {
            OpenCount++;
            if (ThrowOnOpen)
                throw new InvalidOperationException("open failed");

            IsOpen = true;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (ThrowOnRead)
                throw new InvalidOperationException("read failed");

            var read = _reads.Dequeue();
            var bytesToCopy = Math.Min(count, read.Length);
            Array.Copy(read, 0, buffer, offset, bytesToCopy);
            return bytesToCopy;
        }

        public void Close()
        {
            CloseCount++;
            if (ThrowOnClose)
                throw new InvalidOperationException("close failed");

            IsOpen = false;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public void EnqueueRead(byte[] bytes)
        {
            _reads.Enqueue(bytes);
        }

        public void RaiseDataReceived()
        {
            DataReceived?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseErrorReceived(string error)
        {
            ErrorReceived?.Invoke(this, new SerialPortErrorEventArgs(error));
        }
    }
}
