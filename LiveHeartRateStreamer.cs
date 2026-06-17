using System;

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

        public static void Update(int bpm, int zone, string zoneName, string zoneColorHex, bool isTrainingActive, double calories, string duration)
        {
            Current.Bpm = bpm;
            Current.Zone = zone;
            Current.ZoneName = zoneName;
            Current.ZoneColorHex = zoneColorHex;
            Current.IsTrainingActive = isTrainingActive;
            Current.CaloriesBurned = calories;
            Current.DurationString = duration;

            try
            {
                DataUpdated?.Invoke(Current);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Streamer", "Error in streaming event subscriber", ex);
            }
        }
    }
}
