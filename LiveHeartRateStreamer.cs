using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HeartRateMonitor
{
    public class LiveHeartRateData
    {
        public int Bpm { get; set; }
        public int Zone { get; set; }
        public string ZoneName { get; set; } = string.Empty;
        public string ZoneColorHex { get; set; } = string.Empty;
        public bool IsTrainingActive { get; set; }
        public double CaloriesBurned { get; set; }
        public string DurationString { get; set; } = "00:00:00";
    }

    public static class LiveHeartRateStreamer
    {
        public static LiveHeartRateData Current { get; } = new();
        public static event Action<LiveHeartRateData>? DataUpdated;

        private static HttpListener? _httpListener;
        private static bool _isHttpRunning = false;
        private static readonly string _obsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "obs");

        public static void Initialize()
        {
            if (!Directory.Exists(_obsFolderPath))
            {
                Directory.CreateDirectory(_obsFolderPath);
            }
            StartHttpServer();
        }

        public static void Shutdown()
        {
            _isHttpRunning = false;
            _httpListener?.Stop();
            _httpListener?.Close();
        }

        public static void Update(int bpm, int zone, string zoneName, string zoneColorHex, bool isTrainingActive, double calories, string duration)
        {
            Current.Bpm = bpm;
            Current.Zone = zone;
            Current.ZoneName = zoneName;
            Current.ZoneColorHex = zoneColorHex;
            Current.IsTrainingActive = isTrainingActive;
            Current.CaloriesBurned = calories;
            Current.DurationString = duration;

            // Write OBS files asynchronously so we don't block
            Task.Run(() => WriteObsFiles());

            try
            {
                DataUpdated?.Invoke(Current);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Streamer", "Error in streaming event subscriber", ex);
            }
        }

        private static void WriteObsFiles()
        {
            try
            {
                File.WriteAllText(Path.Combine(_obsFolderPath, "OBS_Bpm.txt"), Current.Bpm.ToString());
                File.WriteAllText(Path.Combine(_obsFolderPath, "OBS_Calories.txt"), $"{Current.CaloriesBurned:F0} kcal");
                File.WriteAllText(Path.Combine(_obsFolderPath, "OBS_Zone.txt"), Current.ZoneName);
                File.WriteAllText(Path.Combine(_obsFolderPath, "OBS_Duration.txt"), Current.DurationString);
            }
            catch { /* Ignore brief file locking issues */ }
        }

        private static void StartHttpServer()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add("http://127.0.0.1:8080/api/hr/");
                _httpListener.Start();
                _isHttpRunning = true;
                
                Task.Run(() => ListenAsync());
                AppLogger.Info("Streamer", "HTTP Server started at http://127.0.0.1:8080/api/hr/");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Streamer", "Failed to start HTTP server. It might require admin privileges or the port is in use.", ex);
            }
        }

        private static async Task ListenAsync()
        {
            while (_isHttpRunning && _httpListener != null)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    var response = context.Response;
                    
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.ContentType = "application/json";
                    
                    string json = JsonSerializer.Serialize(Current);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    
                    response.ContentLength64 = buffer.Length;
                    using var output = response.OutputStream;
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped or disposed
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Streamer", "HTTP listener error", ex);
                }
            }
        }
    }
}
