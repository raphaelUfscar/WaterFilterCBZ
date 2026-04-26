using System.Windows;
using WaterFilterCBZ.ViewModels;
using WaterFilterCBZ.Services;
using Serilog;
using OxyPlot.Wpf;

namespace WaterFilterCBZ
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SensorViewModel _viewModel;
        private readonly SerialPortService _serialService;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new SensorViewModel();
            DataContext = _viewModel;

            // Add OxyPlot PlotView to ChartContainer
            var plotView = new PlotView
            {
                Model = _viewModel.PlotModel,
                Foreground = System.Windows.Media.Brushes.Black
            };
            ChartContainer.Children.Add(plotView);

            // Bind PlotModel updates
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SensorViewModel.PlotModel))
                {
                    plotView.Model = _viewModel.PlotModel;
                }
            };

            // Initialize serial port service with COM4 (user-specified)
            _serialService = new SerialPortService("COM4", sample =>
            {
                _viewModel.AddSample(sample);
            });

            // Update connection status display
            _serialService.ConnectionStatusChanged += (s, e) =>
            {
                _viewModel.UpdateConnectionStatus(_serialService.IsConnected, "COM4");
            };

            this.Loaded += (s, e) =>
            {
                _serialService.Connect();
            };
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