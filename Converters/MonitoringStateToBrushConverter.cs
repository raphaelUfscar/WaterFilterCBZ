using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WaterFilterCBZ.Models;

namespace WaterFilterCBZ
{
    /// <summary>
    /// Maps a <see cref="MonitoringState"/> to the brush used for its status indicator
    /// (RC-010 / SRS-C-006). Healthy is green; degraded/fault conditions use warning/error colours
    /// so the operator cannot mistake a degraded state for a healthy one.
    /// </summary>
    public class MonitoringStateToBrushConverter : IValueConverter
    {
        private static readonly Brush Disconnected = Frozen(0x7F, 0x8C, 0x8D); // grey
        private static readonly Brush Connecting = Frozen(0x34, 0x98, 0xDB);   // blue
        private static readonly Brush Healthy = Frozen(0x27, 0xAE, 0x60);      // green
        private static readonly Brush Stale = Frozen(0xE7, 0x4C, 0x3C);        // red
        private static readonly Brush Invalid = Frozen(0xC0, 0x39, 0x2B);      // dark red
        private static readonly Brush ParserError = Frozen(0xE6, 0x7E, 0x22);  // orange
        private static readonly Brush DeviceMismatch = Frozen(0x8E, 0x44, 0xAD); // purple
        private static readonly Brush ProcessingFault = Frozen(0x92, 0x2B, 0x21); // deep red

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is MonitoringState state
                ? state switch
                {
                    MonitoringState.Disconnected => Disconnected,
                    MonitoringState.Connecting => Connecting,
                    MonitoringState.ConnectedHealthy => Healthy,
                    MonitoringState.Stale => Stale,
                    MonitoringState.InvalidValue => Invalid,
                    MonitoringState.ParserError => ParserError,
                    MonitoringState.DeviceMismatch => DeviceMismatch,
                    MonitoringState.ProcessingFault => ProcessingFault,
                    _ => Disconnected,
                }
                : Disconnected;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
