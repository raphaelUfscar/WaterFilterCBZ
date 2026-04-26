using System.IO.Ports;
using System.Collections.Concurrent;
using WaterFilterCBZ.Models;
using Serilog;

namespace WaterFilterCBZ.Services
{
    /// <summary>
    /// Handles serial port communication with the microcontroller.
    /// Reads data on a background thread and invokes callbacks on the UI thread.
    /// </summary>
    public class SerialPortService : IDisposable
    {
        private SerialPort? _port;
        private readonly string _portName;
        private readonly Action<SensorSample> _onSampleReceived;
        private readonly ConcurrentQueue<string> _lineBuffer = new();
        private bool _isDisposed = false;
        private Task? _readTask;
        private CancellationTokenSource _cancellationTokenSource = new();

        public event EventHandler<EventArgs>? ConnectionStatusChanged;

        public bool IsConnected => _port?.IsOpen ?? false;

        public SerialPortService(string portName, Action<SensorSample> onSampleReceived)
        {
            _portName = portName;
            _onSampleReceived = onSampleReceived ?? throw new ArgumentNullException(nameof(onSampleReceived));
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
                    WriteTimeout = 500,
                    NewLine = "\n"
                };

                _port.DataReceived += OnDataReceived;
                _port.ErrorReceived += OnErrorReceived;

                _port.Open();
                Log.Information("Serial port {PortName} opened at {BaudRate} bps", _portName, 115200);

                // Start background read task
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
                string data = _port.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    _lineBuffer.Enqueue(data);
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
            var buffer = string.Empty;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_lineBuffer.TryDequeue(out var chunk))
                    {
                        buffer += chunk;

                        // Split on newline and process complete lines
                        var lines = buffer.Split('\n');
                        for (int i = 0; i < lines.Length - 1; i++)
                        {
                            string line = lines[i].Trim();
                            if (!string.IsNullOrEmpty(line))
                            {
                                if (SensorSample.TryParseCsv(line, out var sample) && sample != null)
                                {
                                    _onSampleReceived(sample);
                                    Log.Debug("Received sensor sample: {Sample}", sample);
                                }
                                else
                                {
                                    Log.Warning("Failed to parse sensor line: {Line}", line);
                                }
                            }
                        }

                        // Keep the incomplete last line in buffer
                        buffer = lines[lines.Length - 1];
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
