using System.IO.Ports;

namespace WaterFilterCBZ.Utils
{
    /// <summary>
    /// Utility for discovering available serial ports.
    /// </summary>
    public static class SerialPortHelper
    {
        /// <summary>
        /// Get all available COM ports on the system.
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Check if a specific port is available.
        /// </summary>
        public static bool IsPortAvailable(string portName)
        {
            var availablePorts = GetAvailablePorts();
            return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
