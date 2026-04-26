using System.Windows;
using WaterFilterCBZ.ViewModels;
using WaterFilterCBZ.Services;
using Serilog;

namespace WaterFilterCBZ
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SensorViewModel _viewModel;
        private SerialPortService? _serialService;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new SensorViewModel();
            DataContext = _viewModel;

            // Subscribe to connection/disconnection commands from ViewModel
            _viewModel.ConnectionStatusChanged += OnConnectRequested;
            _viewModel.DisconnectionStatusChanged += OnDisconnectRequested;

            this.Loaded += (s, e) =>
            {
                // Refresh available ports on startup
                _viewModel.RefreshAvailablePorts();
                
                // Initialize with the selected port
                if (!string.IsNullOrWhiteSpace(_viewModel.SelectedPort))
                {
                    InitializeSerialService(_viewModel.SelectedPort);
                    _serialService?.Connect();
                }
            };
        }

        /// <summary>
        /// Initialize the serial service with a specific COM port.
        /// </summary>
        private void InitializeSerialService(string comPort)
        {
            // Dispose existing service if any
            _serialService?.Dispose();

            // Create new service for the selected port
            _serialService = new SerialPortService(comPort, sample =>
            {
                _viewModel.AddSample(sample);
            });

            // Update connection status display
            _serialService.ConnectionStatusChanged += (s, e) =>
            {
                _viewModel.UpdateConnectionStatus(_serialService.IsConnected, comPort);
            };
        }

        /// <summary>
        /// Handle connect request from ViewModel.
        /// </summary>
        private void OnConnectRequested()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.SelectedPort))
            {
                _viewModel.StatusMessage = "Please select a COM port";
                return;
            }

            try
            {
                // Initialize service for the selected port
                InitializeSerialService(_viewModel.SelectedPort);
                _serialService?.Connect();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to {Port}", _viewModel.SelectedPort);
                _viewModel.StatusMessage = $"Failed to connect: {ex.Message}";
            }
        }

        /// <summary>
        /// Handle disconnect request from ViewModel.
        /// </summary>
        private void OnDisconnectRequested()
        {
            try
            {
                _serialService?.Disconnect();
                _viewModel.UpdateConnectionStatus(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to disconnect");
                _viewModel.StatusMessage = $"Failed to disconnect: {ex.Message}";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _serialService?.Disconnect();
            _serialService?.Dispose();
            Log.Information("Application closing");
        }
    }
}