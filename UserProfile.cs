using System;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace HeartRateMonitor
{
    public class UserProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Użytkownik";
        public string LastDeviceAddress { get; set; } = string.Empty;
        public string LastDeviceName { get; set; } = string.Empty;
        public int Age { get; set; } = 30;
        public double Weight { get; set; } = 75;
        public int RestingHR { get; set; } = 70;
        public string Gender { get; set; } = "Male";
        public string PreferredActivity { get; set; } = "Bieżnia";
        
        // Zone Guard (Training Assistant)
        public bool ZoneGuardEnabled { get; set; } = false;
        public int ZoneGuardTargetZone { get; set; } = 3; // Default Zone 3 (Cardio)
        public int ZoneGuardCheckIntervalSeconds { get; set; } = 30; // Default 30 seconds

        // Training Goals
        public string GoalType { get; set; } = "Brak"; // Brak, Kalorie, Czas
        public int TargetCalories { get; set; } = 500;
        public int TargetDurationMinutes { get; set; } = 45;

        [JsonIgnore]
        public string Initial => !string.IsNullOrEmpty(Name) ? Name.Substring(0, 1).ToUpper() : "U";

        [JsonIgnore]
        public SolidColorBrush ColorBrush => new SolidColorBrush(Color.FromRgb(32, 128, 255)); // Default blue for all avatars now
    }
}
