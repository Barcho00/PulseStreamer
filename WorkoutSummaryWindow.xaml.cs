using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HeartRateMonitor
{
    public partial class WorkoutSummaryWindow : Window
    {
        public WorkoutSummaryWindow(
            string name, 
            string duration, 
            double calories, 
            double avgBpm, 
            double maxBpm, 
            Dictionary<int, int> zoneDurations,
            double epocCalories = 0)
        {
            InitializeComponent();

            TxtWorkoutName.Text = $"Trening: {name}";
            TxtDuration.Text = duration;
            
            // Show calories with EPOC breakdown
            double totalCalories = calories + epocCalories;
            if (epocCalories > 0.5)
            {
                TxtCalories.Text = $"{Math.Round(totalCalories)} kcal";
                TxtEpocValue.Text = $"+{Math.Round(epocCalories)} kcal";
                TxtEpocDetails.Text = $"Trening spalił {Math.Round(calories)} kcal — organizm spali dodatkowe kalorie po wysiłku";
                CardEpoc.Visibility = Visibility.Visible;
            }
            else
            {
                TxtCalories.Text = $"{Math.Round(calories)} kcal";
                CardEpoc.Visibility = Visibility.Collapsed;
            }
            
            TxtAvgBpm.Text = $"{Math.Round(avgBpm)} BPM";
            TxtMaxBpm.Text = $"{Math.Round(maxBpm)} BPM";

            // Calculate total time in zones to show percentages in progress bars
            double totalSeconds = zoneDurations.Values.Sum();
            if (totalSeconds <= 0) totalSeconds = 1.0; // avoid division by zero

            // Zone 1
            int sec1 = zoneDurations.TryGetValue(1, out int s1) ? s1 : 0;
            TxtZone1Time.Text = FormatSeconds(sec1);
            ProgressZone1.Value = (sec1 / totalSeconds) * 100;

            // Zone 2
            int sec2 = zoneDurations.TryGetValue(2, out int s2) ? s2 : 0;
            TxtZone2Time.Text = FormatSeconds(sec2);
            ProgressZone2.Value = (sec2 / totalSeconds) * 100;

            // Zone 3
            int sec3 = zoneDurations.TryGetValue(3, out int s3) ? s3 : 0;
            TxtZone3Time.Text = FormatSeconds(sec3);
            ProgressZone3.Value = (sec3 / totalSeconds) * 100;

            // Zone 4
            int sec4 = zoneDurations.TryGetValue(4, out int s4) ? s4 : 0;
            TxtZone4Time.Text = FormatSeconds(sec4);
            ProgressZone4.Value = (sec4 / totalSeconds) * 100;

            // Zone 5
            int sec5 = zoneDurations.TryGetValue(5, out int s5) ? s5 : 0;
            TxtZone5Time.Text = FormatSeconds(sec5);
            ProgressZone5.Value = (sec5 / totalSeconds) * 100;
        }

        private string FormatSeconds(int seconds)
        {
            if (seconds >= 60)
            {
                int mins = seconds / 60;
                int secs = seconds % 60;
                return $"{mins}m {secs}s";
            }
            return $"{seconds}s";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
