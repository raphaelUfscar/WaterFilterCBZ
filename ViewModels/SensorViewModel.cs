using System.Collections.ObjectModel;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.Utils;
using Serilog;

namespace WaterFilterCBZ.ViewModels
{
    /// <summary>
    /// Main ViewModel for the sensor dashboard.
    /// Manages sensor data, plots, and serial connection state.
    /// </summary>
    public class SensorViewModel : ViewModelBase
    {
        private readonly Dictionary<string, SensorDisplayInfo> _sensorMap = new();
        private int _sampleCount;
        private DateTime _lastUIUpdate = DateTime.Now;
        private const int UI_UPDATE_THRESHOLD_MS = 100; // ~10 FPS max

        private PlotModel _plotModel = new();
        private string _connectionStatus = "Disconnected";
        private string _statusMessage = "Ready";
        private ObservableCollection<string> _availablePorts = new();
        private string? _selectedPort = "COM4";
        private bool _isConnected = false;

        public ObservableCollection<SensorDisplayInfo> Sensors { get; } = new();
        public ObservableCollection<string> AvailablePorts
        {
            get => _availablePorts;
            set => SetProperty(ref _availablePorts, value);
        }

        public string? SelectedPort
        {
            get => _selectedPort;
            set => SetProperty(ref _selectedPort, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public ICommand ClearDataCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        public PlotModel PlotModel
        {
            get => _plotModel;
            set => SetProperty(ref _plotModel, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int SampleCount
        {
            get => _sampleCount;
            set => SetProperty(ref _sampleCount, value);
        }

        public SensorViewModel()
        {
            ClearDataCommand = new RelayCommand(ClearAllData);
            ConnectCommand = new RelayCommand(OnConnect, CanConnect);
            DisconnectCommand = new RelayCommand(OnDisconnect, CanDisconnect);
            InitializeChart();
            RefreshAvailablePorts();
        }

        /// <summary>
        /// Refresh the list of available COM ports.
        /// </summary>
        public void RefreshAvailablePorts()
        {
            var ports = SerialPortHelper.GetAvailablePorts().OrderBy(p => p).ToList();
            
            App.Current?.Dispatcher?.Invoke(() =>
            {
                // Clear and repopulate without losing selection if still valid
                var currentSelection = SelectedPort;
                AvailablePorts.Clear();
                
                foreach (var port in ports)
                {
                    AvailablePorts.Add(port);
                }

                // Restore selection if still available, otherwise select first
                if (!string.IsNullOrEmpty(currentSelection) && AvailablePorts.Contains(currentSelection))
                {
                    SelectedPort = currentSelection;
                }
                else if (AvailablePorts.Count > 0)
                {
                    SelectedPort = AvailablePorts[0];
                }

                StatusMessage = $"Found {AvailablePorts.Count} COM port(s)";
            });
        }

        private bool CanConnect()
        {
            return !IsConnected && !string.IsNullOrWhiteSpace(SelectedPort);
        }

        private bool CanDisconnect()
        {
            return IsConnected;
        }

        private void OnConnect()
        {
            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                StatusMessage = "Please select a COM port";
                return;
            }

            // Trigger connection in MainWindow
            ConnectionStatusChanged?.Invoke();
        }

        private void OnDisconnect()
        {
            // Trigger disconnection in MainWindow
            DisconnectionStatusChanged?.Invoke();
        }

        public event Action? ConnectionStatusChanged;
        public event Action? DisconnectionStatusChanged;

        private void InitializeChart()
        {
            PlotModel = new PlotModel
            {
                Title = "Real-Time Sensor Monitor (4-10 Sensors)",
                Background = OxyColor.FromRgb(240, 240, 240)
            };

            // X-axis: Time
            var xAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                IntervalType = DateTimeIntervalType.Seconds,
                StringFormat = "HH:mm:ss"
            };
            PlotModel.Axes.Add(xAxis);

            // Y-axis: Sensor Value
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Value",
                Key = "Y"
            };
            PlotModel.Axes.Add(yAxis);

            Log.Debug("Chart initialized with axes");
        }

        /// <summary>
        /// Called from SerialPortService when a new sample arrives.
        /// Must be thread-safe across UI and background threads.
        /// </summary>
        public void AddSample(SensorSample sample)
        {
            if (sample == null)
                return;

            try
            {
                // Ensure sensor exists in map
                if (!_sensorMap.TryGetValue(sample.SensorId, out var displayInfo))
                {
                    displayInfo = new SensorDisplayInfo(sample.SensorId);
                    _sensorMap[sample.SensorId] = displayInfo;

                    // Add to observable collection on UI thread
                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        Sensors.Add(displayInfo);
                    });

                    Log.Information("New sensor registered: {SensorId}", sample.SensorId);
                }

                // Update sensor data
                displayInfo.AddValue(sample.Value);
                displayInfo.LastUpdate = sample.Timestamp;
                SampleCount++;

                // Throttle UI updates to ~10 FPS to avoid overwhelming
                var now = DateTime.Now;
                if ((now - _lastUIUpdate).TotalMilliseconds >= UI_UPDATE_THRESHOLD_MS)
                {
                    _lastUIUpdate = now;

                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        UpdateChartForSensor(displayInfo, sample);
                        OnPropertyChanged(nameof(Sensors));
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing sensor sample");
            }
        }

        private void UpdateChartForSensor(SensorDisplayInfo displayInfo, SensorSample sample)
        {
            // Find or create line series for this sensor
            var series = PlotModel.Series
                .OfType<LineSeries>()
                .FirstOrDefault(s => s.Title == displayInfo.SensorId);

            if (series == null)
            {
                series = new LineSeries
                {
                    Title = displayInfo.SensorId,
                    YAxisKey = "Y"
                };
                PlotModel.Series.Add(series);
            }

            // Add data point
            var dateTimeAxis = PlotModel.Axes.OfType<DateTimeAxis>().FirstOrDefault();
            if (dateTimeAxis != null)
            {
                var xValue = DateTimeAxis.ToDouble(sample.Timestamp);
                series.Points.Add(new DataPoint(xValue, sample.Value));

                // Keep only last 300 points per sensor to avoid memory bloat
                if (series.Points.Count > 300)
                {
                    series.Points.RemoveAt(0);
                }
            }

            // Update plot view
            PlotModel.InvalidatePlot(updateData: false);
        }

        public void ClearAllData()
        {
            _sensorMap.Clear();
            Sensors.Clear();
            SampleCount = 0;
            PlotModel.Series.Clear();
            PlotModel.InvalidatePlot(true);

            Log.Information("All sensor data cleared");
            StatusMessage = "Data cleared";
        }

        public void UpdateConnectionStatus(bool isConnected, string? comPort = null)
        {
            IsConnected = isConnected;
            ConnectionStatus = isConnected
                ? $"Connected ({comPort})"
                : "Disconnected";

            StatusMessage = isConnected
                ? "Listening for sensor data..."
                : "No connection";

            Log.Information("Connection status: {Status}", ConnectionStatus);
        }
    }

    /// <summary>
    /// Represents display information for a single sensor.
    /// </summary>
    public class SensorDisplayInfo
    {
        private double _currentValue;
        private double _minValue = double.MaxValue;
        private double _maxValue = double.MinValue;
        private double _avgValue;
        private int _readingCount;

        public string SensorId { get; }
        public DateTime LastUpdate { get; set; }

        public double CurrentValue => _currentValue;
        public double MinValue => _minValue;
        public double MaxValue => _maxValue;
        public double AvgValue => _avgValue;
        public int ReadingCount => _readingCount;

        public SensorDisplayInfo(string sensorId)
        {
            SensorId = sensorId;
            LastUpdate = DateTime.Now;
        }

        public void AddValue(double value)
        {
            _currentValue = value;
            _minValue = Math.Min(_minValue, value);
            _maxValue = Math.Max(_maxValue, value);
            _readingCount++;
            _avgValue = (_avgValue * (_readingCount - 1) + value) / _readingCount;
        }
    }
}
