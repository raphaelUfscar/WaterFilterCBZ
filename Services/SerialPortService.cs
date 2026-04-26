using System.IO.Ports;
using System.Collections.Concurrent;
using System.Linq;
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

        private const byte SOF = 0xAA;
        private const byte EOF = 0x55;
        private const byte ExpectedReceiver = 0x10;

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
                int sofIndex = _receiveBuffer.IndexOf(SOF);
                if (sofIndex < 0)
                {
                    _receiveBuffer.Clear();
                    return;
                }

                if (sofIndex > 0)
                {
                    _receiveBuffer.RemoveRange(0, sofIndex);
                }

                if (_receiveBuffer.Count < 2)
                    return;

                byte payloadLength = _receiveBuffer[1];
                int frameLength = 6 + payloadLength;

                if (_receiveBuffer.Count < frameLength)
                    return;

                if (_receiveBuffer[frameLength - 1] != EOF)
                {
                    Log.Warning("Invalid EOF byte at frame boundary; discarding desynchronized byte.");
                    _receiveBuffer.RemoveAt(0);
                    continue;
                }

                var frame = _receiveBuffer.GetRange(0, frameLength).ToArray();
                if (!ValidateCrc(frame, payloadLength))
                {
                    Log.Warning("CRC validation failed for received frame; discarding first byte.");
                    _receiveBuffer.RemoveAt(0);
                    continue;
                }

                ParseFrame(frame);
                _receiveBuffer.RemoveRange(0, frameLength);
            }
        }

        private bool ValidateCrc(byte[] frame, byte payloadLength)
        {
            int crcOffset = 4 + payloadLength;
            ushort expected = (ushort)(frame[crcOffset] | (frame[crcOffset + 1] << 8));
            ushort actual = ComputeCrcCcitt(frame, 1, 3 + payloadLength);
            return actual == expected;
        }

        private static ushort ComputeCrcCcitt(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + length; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }

            return crc;
        }

        private void ParseFrame(byte[] frame)
        {
            byte length = frame[1];
            byte sender = frame[2];
            byte receiver = frame[3];
            var payload = frame.Skip(4).Take(length).ToArray();

            if (receiver != ExpectedReceiver)
            {
                Log.Debug("Ignoring frame addressed to device ID {Receiver:X2}", receiver);
                return;
            }

            if (payload.Length == 0)
            {
                Log.Warning("Received empty payload in valid frame");
                return;
            }

            switch (payload[0])
            {
                case 0x01:
                    ParseSensorDataPayload(payload);
                    break;
                case 0x02:
                    Log.Debug("Command frame received from sender {Sender:X2}", sender);
                    break;
                case 0x03:
                    Log.Debug("Response frame received from sender {Sender:X2}", sender);
                    break;
                default:
                    Log.Warning("Unknown message type {MessageType:X2}", payload[0]);
                    break;
            }
        }

        private void ParseSensorDataPayload(byte[] payload)
        {
            if (payload.Length < 2)
            {
                Log.Warning("Sensor data payload is too short");
                return;
            }

            byte count = payload[1];
            int expectedLength = 2 + count * 10;
            if (payload.Length != expectedLength)
            {
                Log.Warning("Sensor data payload length mismatch: expected {Expected} bytes, got {Actual} bytes", expectedLength, payload.Length);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int blockStart = 2 + i * 10;
                byte sensorId = payload[blockStart];
                uint timestampMs = BitConverter.ToUInt32(payload, blockStart + 1);
                byte unitId = payload[blockStart + 5];
                float value = BitConverter.ToSingle(payload, blockStart + 6);

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
