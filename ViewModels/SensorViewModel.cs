using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.Services;
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
        private readonly Action _openLogDirectory;
        private readonly Dictionary<string, SensorDisplayInfo> _sensorMap = new();
        private readonly Dictionary<string, int> _sensorPlotIndex = new();
        private readonly PlotModel[] _plots = new PlotModel[4];
        private int _sampleCount;
        private readonly Dictionary<string, DateTime> _lastPlotUpdateBySensor = new();
        private const int PLOT_UPDATE_THRESHOLD_MS = 50; // per-sensor throttle

        // Stale-data supervision (RC-002 / SRS-C-001). Communication-loss timeout is 5 s (OAI-004).
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(5);
        private readonly DispatcherTimer? _staleTimer;

        private string _connectionStatus = "Disconnected";
        private string _statusMessage = "Ready";
        private ObservableCollection<string> _availablePorts = new();
        private string? _selectedPort = "COM4";
        private bool _isConnected = false;
        private bool _hasProcessingFault = false;

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

        /// <summary>
        /// True when the background acquisition/processing task has terminated unexpectedly
        /// (RC-009 / SRS-C-005). Surfaced so the operator does not mistake a dead pipeline for
        /// live monitoring; cleared on a successful reconnect.
        /// </summary>
        public bool HasProcessingFault
        {
            get => _hasProcessingFault;
            private set => SetProperty(ref _hasProcessingFault, value);
        }

        public ICommand ClearDataCommand { get; }
        public ICommand OpenLogsCommand { get; }
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

        public SensorViewModel(Action? openLogDirectory = null)
        {
            _openLogDirectory = openLogDirectory ?? LoggingService.OpenLogDirectory;
            ClearDataCommand = new RelayCommand(ClearAllData);
            OpenLogsCommand = new RelayCommand(OpenLogs);
            ConnectCommand = new RelayCommand(OnConnect, CanConnect);
            DisconnectCommand = new RelayCommand(OnDisconnect, CanDisconnect);
            InitializeCharts();
            RefreshAvailablePorts();

            // Periodically re-evaluate sensor freshness so loss of communication becomes
            // visible to the operator even though no new sample arrives to trigger an update.
            if (App.Current?.Dispatcher != null)
            {
                _staleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _staleTimer.Tick += (_, _) => EvaluateSensorStaleness();
                _staleTimer.Start();
            }
        }

        /// <summary>
        /// Re-evaluates the stale state of every active sensor and logs each transition.
        /// Runs on the UI thread via <see cref="_staleTimer"/>; exposed internally for testing.
        /// </summary>
        internal void EvaluateSensorStaleness()
        {
            var now = DateTime.UtcNow;
            foreach (var sensor in Sensors)
            {
                if (!sensor.EvaluateStaleness(now))
                    continue;

                if (sensor.IsStale)
                {
                    Log.Warning(
                        "Sensor {SensorId} data is stale: no new sample for more than {Seconds}s",
                        sensor.SensorId,
                        sensor.StaleThreshold.TotalSeconds);
                }
                else
                {
                    Log.Information("Sensor {SensorId} data is fresh again", sensor.SensorId);
                }
            }
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
                    displayInfo = new SensorDisplayInfo(
                        sample.SensorId,
                        StaleThreshold,
                        SensorParameterRegistry.ForSensorId(sample.SensorId));
                    _sensorMap[sample.SensorId] = displayInfo;

                    // Add to observable collection on UI thread
                    App.Current?.Dispatcher?.Invoke(() =>
                    {
                        Sensors.Add(displayInfo);
                    });

                    Log.Information("New sensor registered: {SensorId} ({Parameter})", sample.SensorId, displayInfo.DisplayName);
                }

                // Update sensor data (two-tier validation happens inside AddValue)
                var previousState = displayInfo.ValidationState;
                displayInfo.AddValue(sample.Value);
                displayInfo.LastUpdate = sample.Timestamp;
                SampleCount++;

                if (displayInfo.ValidationState != previousState)
                    LogValidationTransition(displayInfo, sample.Value);

                // Do not chart a rejected (implausible) value; it is not a trustworthy measurement.
                if (displayInfo.ValidationState == SensorValidationState.Invalid)
                    return;

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

        private static void LogValidationTransition(SensorDisplayInfo info, double value)
        {
            var p = info.Parameter;
            switch (info.ValidationState)
            {
                case SensorValidationState.Invalid:
                    Log.Warning(
                        "Sensor {SensorId} ({Name}) value {Value} is implausible and was rejected (physical range {Min}..{Max} {Unit}); rejected {RejectedCount} sample(s) total",
                        info.SensorId, info.DisplayName, value, p?.PhysicalMin, p?.PhysicalMax, info.Unit, info.RejectedCount);
                    break;
                case SensorValidationState.OutOfSpec:
                    Log.Warning(
                        "Sensor {SensorId} ({Name}) value {Value} {Unit} is out of operating spec ({Min}..{Max})",
                        info.SensorId, info.DisplayName, value, info.Unit, p?.OperatingMin, p?.OperatingMax);
                    break;
                case SensorValidationState.Normal:
                    Log.Information(
                        "Sensor {SensorId} ({Name}) value is back within operating spec (rejected {RejectedCount} sample(s) total)",
                        info.SensorId, info.DisplayName, info.RejectedCount);
                    break;
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

        private void OpenLogs()
        {
            try
            {
                _openLogDirectory();
                Log.Information("Opened log directory {LogDirectory}", LoggingService.LogDirectory);
                StatusMessage = $"Opened logs: {LoggingService.LogDirectory}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open log folder {LogDirectory}", LoggingService.LogDirectory);
                StatusMessage = $"Log folder: {LoggingService.LogDirectory}";
            }
        }

        public void UpdateConnectionStatus(bool isConnected, string? comPort = null)
        {
            IsConnected = isConnected;

            // A fresh, successful connection clears any prior processing fault (RC-009).
            if (isConnected)
                HasProcessingFault = false;

            ConnectionStatus = isConnected
                ? $"Connected ({comPort})"
                : "Disconnected";

            StatusMessage = isConnected
                ? "Listening for sensor data..."
                : "No connection";

            Log.Information("Connection status: {Status}", ConnectionStatus);
        }

        /// <summary>
        /// Surfaces an unexpected termination of the background acquisition/processing task
        /// (RC-009 / SRS-C-005 / HAZ-004). Marks the session as faulted and degraded so the
        /// operator does not mistake a dead pipeline for live monitoring; recovery requires an
        /// explicit reconnect. Safe to call from a background thread.
        /// </summary>
        public void NotifyProcessingFault(string? detail = null)
        {
            void Apply()
            {
                HasProcessingFault = true;
                IsConnected = false;
                ConnectionStatus = "Processing fault";
                StatusMessage = string.IsNullOrWhiteSpace(detail)
                    ? "Monitoring stopped: background processing fault. Reconnect to resume."
                    : $"Monitoring stopped ({detail}). Reconnect to resume.";
            }

            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(Apply);
            else
                Apply();

            Log.Error("Processing fault surfaced to operator: {Detail}", detail ?? "(no detail)");
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
        private int _rejectedCount;
        private DateTime _lastUpdate;
        private bool _isStale;
        private DateTime _lastSampleAtUtc;
        private SensorValidationState _validationState = SensorValidationState.Normal;

        public string SensorId { get; }

        /// <summary>Process-parameter definition (name, unit, ranges); null for an unknown sensor id.</summary>
        public SensorParameter? Parameter { get; }

        /// <summary>Human-readable label, e.g. "Conductivity (0x01)"; falls back to the raw id.</summary>
        public string DisplayName => Parameter != null ? $"{Parameter.Name} ({SensorId})" : SensorId;

        /// <summary>Engineering unit for the value, or empty when unknown.</summary>
        public string Unit => Parameter?.Unit ?? string.Empty;

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => SetProperty(ref _lastUpdate, value);
        }

        public double CurrentValue => _currentValue;
        public string CurrentValueText => string.IsNullOrEmpty(Unit)
            ? _currentValue.ToString("F2")
            : $"{_currentValue:F2} {Unit}";
        // Until at least one value is accepted, the running min/max still hold their
        // initialization sentinels (double.MaxValue / double.MinValue); report 0 instead
        // so the UI never shows those sentinels (e.g. when every sample is rejected).
        public double MinValue => _readingCount == 0 ? 0.0 : _minValue;
        public double MaxValue => _readingCount == 0 ? 0.0 : _maxValue;
        public double AvgValue => _avgValue;
        public int ReadingCount => _readingCount;

        /// <summary>
        /// Cumulative number of samples rejected for this sensor as physically implausible
        /// (RC-008 / SRS-C-003). Provides an audit trail without logging every rejected sample.
        /// </summary>
        public int RejectedCount => _rejectedCount;

        /// <summary>
        /// Two-tier validation result for the most recent sample (RC-008 / SRS-C-003).
        /// </summary>
        public SensorValidationState ValidationState
        {
            get => _validationState;
            private set => SetProperty(ref _validationState, value);
        }

        /// <summary>
        /// Maximum age of the most recent sample before this sensor is considered stale.
        /// Risk control RC-002 / requirement SRS-C-001. Default 5 s (OAI-004).
        /// </summary>
        public TimeSpan StaleThreshold { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// True when no new sample has arrived within <see cref="StaleThreshold"/>.
        /// Surfaced to the operator so a stale reading is not mistaken for a live one.
        /// </summary>
        public bool IsStale
        {
            get => _isStale;
            private set => SetProperty(ref _isStale, value);
        }

        /// <summary>UTC time at which the most recent sample was received (wall-clock, used for freshness).</summary>
        public DateTime LastSampleAtUtc => _lastSampleAtUtc;

        public SensorDisplayInfo(string sensorId, TimeSpan? staleThreshold = null, SensorParameter? parameter = null)
        {
            SensorId = sensorId;
            Parameter = parameter;
            _lastUpdate = DateTime.Now;
            _lastSampleAtUtc = DateTime.UtcNow;
            if (staleThreshold.HasValue)
                StaleThreshold = staleThreshold.Value;
        }

        public void AddValue(double value)
        {
            // A sample arrived: the link is alive, so clear any stale state regardless of value.
            _lastSampleAtUtc = DateTime.UtcNow;
            IsStale = false;

            // Tier 1 (RC-008 / SRS-C-003): physically implausible / corrupt values are rejected.
            // The last good value and statistics are preserved so the operator is not shown garbage.
            if (Parameter != null && !Parameter.IsPhysicallyPlausible(value))
            {
                _rejectedCount++;
                OnPropertyChanged(nameof(RejectedCount));
                ValidationState = SensorValidationState.Invalid;
                return;
            }

            _currentValue = value;
            _minValue = Math.Min(_minValue, value);
            _maxValue = Math.Max(_maxValue, value);
            _readingCount++;
            _avgValue = (_avgValue * (_readingCount - 1) + value) / _readingCount;

            // Tier 2: plausible but outside the operating specification → flag, still display.
            ValidationState = (Parameter != null && !Parameter.IsWithinOperatingSpec(value))
                ? SensorValidationState.OutOfSpec
                : SensorValidationState.Normal;

            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(CurrentValueText));
            OnPropertyChanged(nameof(MinValue));
            OnPropertyChanged(nameof(MaxValue));
            OnPropertyChanged(nameof(AvgValue));
            OnPropertyChanged(nameof(ReadingCount));
        }

        /// <summary>
        /// Re-evaluates the stale state against the supplied UTC time. A sensor with no
        /// readings is never stale. Returns true when the stale state changed (for logging).
        /// </summary>
        public bool EvaluateStaleness(DateTime utcNow)
        {
            bool shouldBeStale = _readingCount > 0 && (utcNow - _lastSampleAtUtc) > StaleThreshold;
            if (shouldBeStale == IsStale)
                return false;

            IsStale = shouldBeStale;
            return true;
        }
    }
}
