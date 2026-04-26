using System.IO.Ports;
using System.Collections.Concurrent;
using WaterFilterCBZ.Models;
using Serilog;

namespace WaterFilterCBZ.Services
{
    /// <summary>
    /// Handles serial port communication with the microcontroller.
    /// Reads framed binary packets, validates CRC, and dispatches samples.
    /// </summary>
    public class SerialPortService : IDisposable
    {
        private SerialPort? _port;
        private string _portName;
        private readonly Action<SensorSample> _onSampleReceived;
        private readonly ConcurrentQueue<byte[]> _byteQueue = new();
        private readonly List<byte> _receiveBuffer = new();
        private bool _isDisposed = false;
        private Task? _readTask;
        private CancellationTokenSource _cancellationTokenSource = new();

        private const byte START_BYTE = 0xAA;
        private const byte END_BYTE = 0x55;
        private const int SENSOR_ENTRY_SIZE_BYTES = 10; // sensor_id(1) + timestamp(4) + unit_id(1) + float(4)
        private const int MAX_SENSORS = 4;
        private static readonly TimeSpan FrameAssemblyTimeout = TimeSpan.FromMilliseconds(350);
        private DateTime? _sawStartByteAtUtc;

        public event EventHandler<EventArgs>? ConnectionStatusChanged;

        public bool IsConnected => _port?.IsOpen ?? false;

        public SerialPortService(string portName, Action<SensorSample> onSampleReceived)
        {
            _portName = portName;
            _onSampleReceived = onSampleReceived ?? throw new ArgumentNullException(nameof(onSampleReceived));
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
            if (IsConnected)
                return;

            try
            {
                _port = new SerialPort(_portName, 115200)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _port.DataReceived += OnDataReceived;
                _port.ErrorReceived += OnErrorReceived;

                _port.Open();
                Log.Information("Serial port {PortName} opened at {BaudRate} bps", _portName, 115200);

                _cancellationTokenSource = new CancellationTokenSource();
                _readTask = Task.Run(() => ProcessIncomingDataAsync(_cancellationTokenSource.Token));

                OnConnectionStatusChanged();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open serial port {PortName}", _portName);
                OnConnectionStatusChanged();
            }
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _readTask?.Wait(TimeSpan.FromSeconds(2));

                if (_port != null)
                {
                    _port.DataReceived -= OnDataReceived;
                    _port.ErrorReceived -= OnErrorReceived;
                    _port.Close();
                    _port.Dispose();
                    _port = null;
                }

                Log.Information("Serial port {PortName} closed", _portName);
                OnConnectionStatusChanged();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing serial port {PortName}", _portName);
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_port == null || !_port.IsOpen)
                return;

            try
            {
                int bytesToRead = _port.BytesToRead;
                if (bytesToRead <= 0)
                    return;

                var chunk = new byte[bytesToRead];
                int read = _port.Read(chunk, 0, bytesToRead);
                if (read > 0)
                {
                    if (read != chunk.Length)
                    {
                        Array.Resize(ref chunk, read);
                    }

                    _byteQueue.Enqueue(chunk);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reading from serial port");
            }
        }

        private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Log.Error("Serial port error: {Error}", e.EventType);
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

        private static DateTime DecodeTimestamp(uint timestampMs)
        {
            // MCU timestamp is sent as a 32-bit millisecond counter.
            // The app uses arrival time for display because absolute epoch
            // values cannot be reliably represented in a 32-bit millisecond field.
            return DateTime.Now;
        }

        private void OnConnectionStatusChanged()
        {
            ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Disconnect();
            _cancellationTokenSource?.Dispose();
            _port?.Dispose();
            _isDisposed = true;
        }
    }
}
