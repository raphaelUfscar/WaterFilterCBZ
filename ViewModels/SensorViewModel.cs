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
        private readonly Dictionary<string, int> _sensorPlotIndex = new();
        private readonly PlotModel[] _plots = new PlotModel[4];
        private int _sampleCount;
        private readonly Dictionary<string, DateTime> _lastPlotUpdateBySensor = new();
        private const int PLOT_UPDATE_THRESHOLD_MS = 50; // per-sensor throttle

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

        public PlotModel PlotModel1 => _plots[0];
        public PlotModel PlotModel2 => _plots[1];
        public PlotModel PlotModel3 => _plots[2];
        public PlotModel PlotModel4 => _plots[3];

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
            InitializeCharts();
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

        private void InitializeCharts()
        {
            for (int i = 0; i < _plots.Length; i++)
            {
                _plots[i] = new PlotModel
                {
                    Title = $"Sensor {i + 1}",
                    Background = OxyColor.FromRgb(250, 250, 250)
                };

                _plots[i].Axes.Add(new DateTimeAxis
                {
                    Position = AxisPosition.Bottom,
                    Title = "Time",
                    IntervalType = DateTimeIntervalType.Seconds,
                    StringFormat = "HH:mm:ss"
                });

                _plots[i].Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left,
                    Title = "Value"
                });
            }

            OnPropertyChanged(nameof(PlotModel1));
            OnPropertyChanged(nameof(PlotModel2));
            OnPropertyChanged(nameof(PlotModel3));
            OnPropertyChanged(nameof(PlotModel4));

            Log.Debug("Charts initialized (2x2)");
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

                // Update the correct chart for this sensor (per-sensor throttled)
                var now = DateTime.Now;
                if (!_lastPlotUpdateBySensor.TryGetValue(sample.SensorId, out var lastPlotUpdate) ||
                    (now - lastPlotUpdate).TotalMilliseconds >= PLOT_UPDATE_THRESHOLD_MS)
                {
                    _lastPlotUpdateBySensor[sample.SensorId] = now;

                    App.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        UpdateChartForSensor(displayInfo, sample);
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
            if (!_sensorPlotIndex.TryGetValue(displayInfo.SensorId, out var plotIndex))
            {
                plotIndex = _sensorPlotIndex.Count;
                if (plotIndex >= _plots.Length)
                {
                    // Ignore sensors beyond 4 plots
                    return;
                }

                _sensorPlotIndex[displayInfo.SensorId] = plotIndex;
                _plots[plotIndex].Title = $"Sensor {displayInfo.SensorId}";
            }

            var model = _plots[plotIndex];

            // Find or create series for this sensor (one per plot)
            var series = model.Series.OfType<LineSeries>().FirstOrDefault();

            if (series == null)
            {
                series = new LineSeries
                {
                    Title = displayInfo.SensorId
                };
                model.Series.Add(series);
            }

            // Add data point
            var dateTimeAxis = model.Axes.OfType<DateTimeAxis>().FirstOrDefault();
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
            model.InvalidatePlot(updateData: true);

            Log.Debug(
                "Graph updated: sensor={SensorId} plotIndex={PlotIndex} points={PointCount} value={Value:F3} time={Timestamp:HH:mm:ss.fff}",
                displayInfo.SensorId,
                plotIndex,
                series.Points.Count,
                sample.Value,
                sample.Timestamp);
        }

        public void ClearAllData()
        {
            _sensorMap.Clear();
            _sensorPlotIndex.Clear();
            _lastPlotUpdateBySensor.Clear();
            Sensors.Clear();
            SampleCount = 0;
            foreach (var model in _plots)
            {
                model.Series.Clear();
                model.Title = "Sensor";
                model.InvalidatePlot(true);
            }

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
    public class SensorDisplayInfo : ViewModelBase
    {
        private double _currentValue;
        private double _minValue = double.MaxValue;
        private double _maxValue = double.MinValue;
        private double _avgValue;
        private int _readingCount;
        private DateTime _lastUpdate;

        public string SensorId { get; }
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => SetProperty(ref _lastUpdate, value);
        }

        public double CurrentValue => _currentValue;
        public double MinValue => _minValue;
        public double MaxValue => _maxValue;
        public double AvgValue => _avgValue;
        public int ReadingCount => _readingCount;

        public SensorDisplayInfo(string sensorId)
        {
            SensorId = sensorId;
            _lastUpdate = DateTime.Now;
        }

        public void AddValue(double value)
        {
            _currentValue = value;
            _minValue = Math.Min(_minValue, value);
            _maxValue = Math.Max(_maxValue, value);
            _readingCount++;
            _avgValue = (_avgValue * (_readingCount - 1) + value) / _readingCount;

            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(MinValue));
            OnPropertyChanged(nameof(MaxValue));
            OnPropertyChanged(nameof(AvgValue));
            OnPropertyChanged(nameof(ReadingCount));
        }
    }
}
