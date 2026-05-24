using System.IO.Ports;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using WaterFilterCBZ.Models;
using Serilog;

namespace WaterFilterCBZ.Services
{
    /// <summary>
    /// Interface for Serial Port communication to allow for mocking during tests.
    /// </summary>
    public interface ISerialPortService : IDisposable
    {
        bool IsConnected { get; }
        event EventHandler<EventArgs>? ConnectionStatusChanged;
        void Connect();
        void Disconnect();
        void SetPort(string portName);
    }

    internal sealed class SerialPortErrorEventArgs(string error) : EventArgs
    {
        public string Error { get; } = error;
    }

    internal interface ISerialConnection : IDisposable
    {
        bool IsOpen { get; }
        int BytesToRead { get; }
        event EventHandler<EventArgs>? DataReceived;
        event EventHandler<SerialPortErrorEventArgs>? ErrorReceived;
        void Open();
        int Read(byte[] buffer, int offset, int count);
        void Close();
    }

    [ExcludeFromCodeCoverage]
    internal sealed class SerialPortConnection : ISerialConnection
    {
        private readonly SerialPort _port;

        public SerialPortConnection(string portName, int baudRate)
        {
            _port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            _port.DataReceived += (_, _) => DataReceived?.Invoke(this, EventArgs.Empty);
            _port.ErrorReceived += (_, e) => ErrorReceived?.Invoke(this, new SerialPortErrorEventArgs(e.EventType.ToString()));
        }

        public bool IsOpen => _port.IsOpen;
        public int BytesToRead => _port.BytesToRead;
        public event EventHandler<EventArgs>? DataReceived;
        public event EventHandler<SerialPortErrorEventArgs>? ErrorReceived;

        public void Open() => _port.Open();

        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);

        public void Close() => _port.Close();

        public void Dispose() => _port.Dispose();
    }

    /// <summary>
    /// Handles serial port communication with the microcontroller.
    /// Reads framed binary packets, validates CRC, and dispatches samples.
    /// </summary>
    public class SerialPortService : ISerialPortService
    {
        private ISerialConnection? _port;
        private string _portName;
        private readonly Action<SensorSample> _onSampleReceived;
        private readonly Func<string, int, ISerialConnection> _connectionFactory;
        private readonly ConcurrentQueue<byte[]> _byteQueue = new();
        private readonly List<byte> _receiveBuffer = new();
        private readonly object _connectionLock = new();
        private bool _isDisposed = false;
        private Task? _readTask;
        private Task? _heartbeatTask;
        private CancellationTokenSource? _connectionCancellationTokenSource;
        private bool _manualDisconnectRequested = true;
        private int _reconnectInProgress;
        private DateTime _lastDataReceivedUtc = DateTime.MinValue;
        private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(2);
        private TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);
        private TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);

        public int BaudRate { get; set; } = 115200;
        public bool AutoReconnectEnabled { get; set; } = true;

        public TimeSpan HeartbeatInterval
        {
            get => _heartbeatInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), "Heartbeat interval must be greater than zero.");

                _heartbeatInterval = value;
            }
        }

        public TimeSpan HeartbeatTimeout
        {
            get => _heartbeatTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), "Heartbeat timeout must be greater than zero.");

                _heartbeatTimeout = value;
            }
        }

        public TimeSpan ReconnectDelay
        {
            get => _reconnectDelay;
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), "Reconnect delay cannot be negative.");

                _reconnectDelay = value;
            }
        }

        private const byte START_BYTE = 0xAA;
        private const byte END_BYTE = 0x55;
        private const int SENSOR_ENTRY_SIZE_BYTES = 10; // sensor_id(1) + timestamp(4) + unit_id(1) + float(4)
        private const int MAX_SENSORS = 4;
        private static readonly TimeSpan FrameAssemblyTimeout = TimeSpan.FromMilliseconds(350);
        private DateTime? _sawStartByteAtUtc;

        public event EventHandler<EventArgs>? ConnectionStatusChanged;

        public bool IsConnected => _port?.IsOpen ?? false;

        public SerialPortService(string portName, Action<SensorSample> onSampleReceived)
            : this(portName, onSampleReceived, (name, baudRate) => new SerialPortConnection(name, baudRate))
        {
        }

        internal SerialPortService(
            string portName,
            Action<SensorSample> onSampleReceived,
            Func<string, int, ISerialConnection> connectionFactory)
        {
            _portName = portName;
            _onSampleReceived = onSampleReceived ?? throw new ArgumentNullException(nameof(onSampleReceived));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        /// <summary>
        /// Change the serial port. Will disconnect from current port if connected.
        /// </summary>
        public void SetPort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentException("Port name cannot be null or empty", nameof(portName));

            if (_portName == portName && IsConnected)
                return;

            // Disconnect from current port if needed
            if (IsConnected)
                Disconnect();

            _portName = portName;
            Log.Information("Serial port set to {PortName}", _portName);
        }

        public void Connect()
        {
            lock (_connectionLock)
            {
                _manualDisconnectRequested = false;

                if (IsConnected)
                    return;

                TryOpenPort();
            }
        }

        public void Disconnect()
        {
            _manualDisconnectRequested = true;
            ClosePort(raiseStatusChanged: true);
        }

        private bool TryOpenPort()
        {
            ISerialConnection? port = null;

            try
            {
                ClosePort(raiseStatusChanged: false);

                port = _connectionFactory(_portName, BaudRate);
                port.DataReceived += OnDataReceived;
                port.ErrorReceived += OnErrorReceived;
                port.Open();

                _port = port;
                _lastDataReceivedUtc = DateTime.UtcNow;
                _connectionCancellationTokenSource = new CancellationTokenSource();

                var token = _connectionCancellationTokenSource.Token;
                _readTask = Task.Run(() => ProcessIncomingDataAsync(token), token);
                _heartbeatTask = Task.Run(() => MonitorHeartbeatAsync(token), token);

                Log.Information("Serial port {PortName} opened at {BaudRate} bps", _portName, BaudRate);
                OnConnectionStatusChanged();
                port = null;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open serial port {PortName}", _portName);
                if (port != null)
                {
                    port.DataReceived -= OnDataReceived;
                    port.ErrorReceived -= OnErrorReceived;
                    port.Dispose();
                }

                ClosePort(raiseStatusChanged: false);
                OnConnectionStatusChanged();
                return false;
            }
        }

        private void ClosePort(bool raiseStatusChanged)
        {
            ISerialConnection? portToClose;
            CancellationTokenSource? cancellationTokenSource;
            Task? readTask;
            Task? heartbeatTask;
            bool wasConnected;

            lock (_connectionLock)
            {
                portToClose = _port;
                cancellationTokenSource = _connectionCancellationTokenSource;
                readTask = _readTask;
                heartbeatTask = _heartbeatTask;
                wasConnected = portToClose?.IsOpen ?? false;

                _port = null;
                _connectionCancellationTokenSource = null;
                _readTask = null;
                _heartbeatTask = null;
                _byteQueue.Clear();
                _receiveBuffer.Clear();
                _sawStartByteAtUtc = null;
            }

            try
            {
                cancellationTokenSource?.Cancel();
                WaitForTaskToStop(readTask);
                WaitForTaskToStop(heartbeatTask);

                if (portToClose != null)
                {
                    portToClose.DataReceived -= OnDataReceived;
                    portToClose.ErrorReceived -= OnErrorReceived;

                    if (portToClose.IsOpen)
                    {
                        portToClose.Close();
                    }

                    portToClose.Dispose();
                    Log.Information("Serial port {PortName} closed", _portName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing serial port {PortName}", _portName);
            }
            finally
            {
                cancellationTokenSource?.Dispose();
            }

            if (raiseStatusChanged && wasConnected)
                OnConnectionStatusChanged();
        }

        private static void WaitForTaskToStop(Task? task)
        {
            if (task == null || task.Id == Task.CurrentId)
                return;

            try
            {
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException or TaskCanceledException))
            {
                // Expected when shutting down the read and heartbeat tasks.
            }
        }

        private void OnDataReceived(object? sender, EventArgs e)
        {
            if (sender is not ISerialConnection port || !port.IsOpen)
                return;

            try
            {
                int bytesToRead = port.BytesToRead;
                if (bytesToRead <= 0)
                    return;

                var chunk = new byte[bytesToRead];
                int read = port.Read(chunk, 0, bytesToRead);
                if (read > 0)
                {
                    if (read != chunk.Length)
                    {
                        Array.Resize(ref chunk, read);
                    }

                    _lastDataReceivedUtc = DateTime.UtcNow;
                    _byteQueue.Enqueue(chunk);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading from serial port");
                ScheduleReconnect("read failure");
            }
        }

        private void OnErrorReceived(object? sender, SerialPortErrorEventArgs e)
        {
            Log.Error("Serial port error: {Error}", e.Error);
            ScheduleReconnect($"serial error {e.Error}");
        }

        private async Task ProcessIncomingDataAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_byteQueue.TryDequeue(out var chunk))
                    {
                        _receiveBuffer.AddRange(chunk);
                        ParseReceiveBuffer();
                    }
                    else
                    {
                        await Task.Delay(10, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in serial port processing task");
                ScheduleReconnect("processing failure");
            }
        }

        private async Task MonitorHeartbeatAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(HeartbeatInterval, ct);

                    if (!IsConnected)
                        continue;

                    var idleTime = DateTime.UtcNow - _lastDataReceivedUtc;
                    if (idleTime > HeartbeatTimeout)
                    {
                        Log.Warning(
                            "Serial heartbeat timeout on {PortName}; no data received for {IdleSeconds:F1}s.",
                            _portName,
                            idleTime.TotalSeconds);
                        ScheduleReconnect("heartbeat timeout");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting or reconnecting.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in serial heartbeat monitor");
                ScheduleReconnect("heartbeat monitor failure");
            }
        }

        private void ScheduleReconnect(string reason)
        {
            if (!AutoReconnectEnabled || _manualDisconnectRequested || _isDisposed)
                return;

            if (Interlocked.CompareExchange(ref _reconnectInProgress, 1, 0) != 0)
                return;

            _ = Task.Run(() => ReconnectAsync(reason));
        }

        private async Task ReconnectAsync(string reason)
        {
            try
            {
                Log.Warning("Scheduling serial reconnect for {PortName}: {Reason}", _portName, reason);
                ClosePort(raiseStatusChanged: true);

                while (!_manualDisconnectRequested && !_isDisposed)
                {
                    await Task.Delay(ReconnectDelay);

                    if (_manualDisconnectRequested || _isDisposed)
                        return;

                    lock (_connectionLock)
                    {
                        if (IsConnected || TryOpenPort())
                            return;
                    }

                    Log.Warning("Serial reconnect attempt failed for {PortName}; retrying.", _portName);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectInProgress, 0);
            }
        }

        private void ParseReceiveBuffer()
        {
            while (true)
            {
                int startIndex = _receiveBuffer.IndexOf(START_BYTE);
                if (startIndex < 0)
                {
                    _receiveBuffer.Clear();
                    _sawStartByteAtUtc = null;
                    return;
                }

                if (startIndex > 0)
                {
                    _receiveBuffer.RemoveRange(0, startIndex);
                }

                if (_receiveBuffer.Count < 2)
                {
                    _sawStartByteAtUtc ??= DateTime.UtcNow;
                    return;
                }

                _sawStartByteAtUtc ??= DateTime.UtcNow;
                if (_sawStartByteAtUtc.HasValue && DateTime.UtcNow - _sawStartByteAtUtc.Value > FrameAssemblyTimeout)
                {
                    Log.Warning("Frame assembly timeout; resetting parser state.");
                    _receiveBuffer.Clear();
                    _sawStartByteAtUtc = null;
                    return;
                }

                byte count = _receiveBuffer[1];
                if (count == 0 || count > MAX_SENSORS)
                {
                    Log.Warning("Corrupted frame (invalid sensor count={Count}); resyncing.", count);
                    ResyncToNextStartByte(dropCurrentStartByte: true);
                    continue;
                }

                int frameLength = 4 + count * SENSOR_ENTRY_SIZE_BYTES; // 1(start)+1(count)+count*10+1(checksum)+1(end)
                if (_receiveBuffer.Count < frameLength)
                    return;

                if (_receiveBuffer[frameLength - 1] != END_BYTE)
                {
                    Log.Warning("Invalid END byte at frame boundary; resyncing.");
                    ResyncToNextStartByte(dropCurrentStartByte: true);
                    continue;
                }

                var frame = _receiveBuffer.GetRange(0, frameLength).ToArray();
                if (!ValidateChecksum(frame))
                {
                    Log.Warning("Checksum validation failed; resyncing.");
                    ResyncToNextStartByte(dropCurrentStartByte: true);
                    continue;
                }

                ParseFrame(frame);
                _receiveBuffer.RemoveRange(0, frameLength);
                _sawStartByteAtUtc = null;
            }
        }

        private void ResyncToNextStartByte(bool dropCurrentStartByte)
        {
            int searchFrom = dropCurrentStartByte ? 1 : 0;
            int nextStart = _receiveBuffer.IndexOf(START_BYTE, searchFrom);
            if (nextStart < 0)
            {
                _receiveBuffer.Clear();
                _sawStartByteAtUtc = null;
                return;
            }

            if (nextStart > 0)
            {
                _receiveBuffer.RemoveRange(0, nextStart);
            }

            _sawStartByteAtUtc = DateTime.UtcNow;
        }

        private static bool ValidateChecksum(byte[] frame)
        {
            if (frame.Length < 4)
                return false;

            if (frame[0] != START_BYTE || frame[^1] != END_BYTE)
                return false;

            byte expected = frame[^2];

            int sum = 0;
            // sum of bytes from 0xAA through last payload byte (excluding checksum and end byte)
            for (int i = 0; i <= frame.Length - 3; i++)
            {
                sum = (sum + frame[i]) & 0xFF;
            }

            return (byte)sum == expected;
        }

        private void ParseFrame(byte[] frame)
        {
            if (frame.Length < 4 || frame[0] != START_BYTE || frame[^1] != END_BYTE)
                return;

            byte count = frame[1];
            if (count == 0 || count > MAX_SENSORS)
                return;

            int expectedFrameLength = 4 + count * SENSOR_ENTRY_SIZE_BYTES;
            if (frame.Length != expectedFrameLength)
                return;

            for (int i = 0; i < count; i++)
            {
                int entryStart = 2 + i * SENSOR_ENTRY_SIZE_BYTES;
                byte sensorId = frame[entryStart];
                uint timestampMs = BitConverter.ToUInt32(frame, entryStart + 1);
                byte unitId = frame[entryStart + 5];
                float value = BitConverter.ToSingle(frame, entryStart + 6);

                var sample = new SensorSample
                {
                    SensorId = $"0x{sensorId:X2}",
                    Timestamp = DecodeTimestamp(timestampMs),
                    Value = value
                };

                _onSampleReceived(sample);
                Log.Debug("Received sensor sample: {Sample} unit={UnitId}", sample, unitId);
            }
        }

        private DateTime? _mcuStartTime;
        private uint _firstMcuTimestamp;

        private DateTime DecodeTimestamp(uint timestampMs)
        {
            // Professional approach: Sync MCU internal clock with PC wall-clock on first packet
            if (!_mcuStartTime.HasValue)
            {
                _mcuStartTime = DateTime.Now;
                _firstMcuTimestamp = timestampMs;
                return _mcuStartTime.Value;
            }

            long diffMs = (long)timestampMs - _firstMcuTimestamp;
            return _mcuStartTime.Value.AddMilliseconds(diffMs);
        }

        private void OnConnectionStatusChanged()
        {
            ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                Disconnect();
                _connectionCancellationTokenSource?.Dispose();
                _port?.Dispose();
            }

            _isDisposed = true;
        }
    }
}
