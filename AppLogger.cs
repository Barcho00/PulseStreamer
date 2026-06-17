using System;
using System.IO;
using System.Text;

namespace HeartRateMonitor
{
    /// <summary>
    /// Simple file-based logger that writes timestamped entries to a rolling log file.
    /// Log file: {AppDirectory}/logs/app_{date}.log
    /// </summary>
    public static class AppLogger
    {
        private static readonly object _lock = new();
        private static string? _logDirectory;
        private static string? _currentLogPath;
        private static DateTime _currentLogDate;

        /// <summary>
        /// Initializes the logger with the application base directory.
        /// Creates the logs directory if it doesn't exist.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(_logDirectory);
                _currentLogDate = DateTime.Now.Date;
                _currentLogPath = GetLogFilePath(_currentLogDate);
                Info("AppLogger", "Logger initialized");
                Info("AppLogger", $"Log file: {_currentLogPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize AppLogger: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the path to the current log file (useful for diagnostics).
        /// </summary>
        public static string? CurrentLogPath => _currentLogPath;

        private static string GetLogFilePath(DateTime date)
        {
            return Path.Combine(_logDirectory!, $"app_{date:yyyy-MM-dd}.log");
        }

        private static void EnsureCurrentLogPath()
        {
            if (DateTime.Now.Date != _currentLogDate)
            {
                _currentLogDate = DateTime.Now.Date;
                _currentLogPath = GetLogFilePath(_currentLogDate);
            }
        }

        private static void WriteLog(string level, string category, string message)
        {
            if (_logDirectory == null) return;

            try
            {
                lock (_lock)
                {
                    EnsureCurrentLogPath();
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string line = $"[{timestamp}] [{level}] [{category}] {message}{Environment.NewLine}";
                    File.AppendAllText(_currentLogPath!, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging should never crash the application
            }
        }

        public static void Info(string category, string message)
        {
            WriteLog("INFO", category, message);
        }

        public static void Warn(string category, string message)
        {
            WriteLog("WARN", category, message);
        }

        public static void Error(string category, string message)
        {
            WriteLog("ERROR", category, message);
        }

        public static void Error(string category, string message, Exception ex)
        {
            WriteLog("ERROR", category, $"{message} | Exception: {ex.GetType().Name}: {ex.Message}");
        }

        public static void Debug(string category, string message)
        {
            WriteLog("DEBUG", category, message);
        }
    }
}
