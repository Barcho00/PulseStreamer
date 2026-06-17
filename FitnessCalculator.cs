using System;
using System.Windows.Media;

namespace HeartRateMonitor
{
    public static class FitnessCalculator
    {
        public static double CalculateMaxHeartRate(int age)
        {
            return 220.0 - age;
        }

        public static int GetHeartRateZone(int bpm, int age)
        {
            double maxHr = CalculateMaxHeartRate(age);
            if (maxHr <= 0) return 0;

            double pct = (double)bpm / maxHr;

            if (pct >= 0.90) return 5;
            if (pct >= 0.80) return 4;
            if (pct >= 0.70) return 3;
            if (pct >= 0.60) return 2;
            if (pct >= 0.50) return 1;

            return 0; // Odpoczynek / brak strefy
        }

        public static string GetZoneName(int zone)
        {
            return zone switch
            {
                1 => "Rozgrzewka",
                2 => "Spalanie Tłuszczu",
                3 => "Cardio / Aerobowa",
                4 => "Próg Beztlenowy",
                5 => "Ekstremalny Wysiłek",
                _ => "Odpoczynek"
            };
        }

        public static Color GetZoneColor(int zone)
        {
            return zone switch
            {
                1 => Color.FromRgb(52, 152, 219),  // Blue
                2 => Color.FromRgb(241, 196, 15),  // Yellow/Amber
                3 => Color.FromRgb(230, 126, 34),  // Orange
                4 => Color.FromRgb(231, 76, 60),   // Red
                5 => Color.FromRgb(155, 89, 182),  // Purple
                _ => Color.FromRgb(127, 140, 141)  // Grey
            };
        }

        public static (int Low, int High) GetZoneLimits(int zone, int age)
        {
            double maxHr = CalculateMaxHeartRate(age);
            return zone switch
            {
                1 => ((int)Math.Round(maxHr * 0.5), (int)Math.Round(maxHr * 0.6)),
                2 => ((int)Math.Round(maxHr * 0.6), (int)Math.Round(maxHr * 0.7)),
                3 => ((int)Math.Round(maxHr * 0.7), (int)Math.Round(maxHr * 0.8)),
                4 => ((int)Math.Round(maxHr * 0.8), (int)Math.Round(maxHr * 0.9)),
                5 => ((int)Math.Round(maxHr * 0.9), (int)Math.Round(maxHr)),
                _ => (0, (int)Math.Round(maxHr * 0.5))
            };
        }

        public static double CalculateCaloriesBurned(int bpm, double weight, double age, bool isMale, string activity, double elapsedSeconds)
        {
            if (elapsedSeconds <= 0) return 0;

            double ee_kcal_min;

            if (bpm >= 85)
            {
                // Keytel Formula
                if (isMale)
                {
                    ee_kcal_min = (-55.0969 + 0.6309 * bpm + 0.1988 * weight + 0.2017 * age) / 4.184;
                }
                else
                {
                    ee_kcal_min = (-20.4022 + 0.4472 * bpm - 0.1263 * weight + 0.074 * age) / 4.184;
                }

                // Apply Activity Factor
                double factor = activity switch
                {
                    "Aerobik" => 1.00,
                    "VR Fitness" => 0.80,
                    "Bieżnia" => 1.05,
                    "Boks" => 1.15,
                    "Rower" => 0.95,
                    _ => 1.00
                };
                ee_kcal_min *= factor;
            }
            else
            {
                // MET Formula
                double met = activity switch
                {
                    "Aerobik" => 6.5,
                    "VR Fitness" => 3.5,
                    "Bieżnia" => 8.0,
                    "Boks" => 9.0,
                    "Rower" => 7.0,
                    _ => 5.0
                };
                ee_kcal_min = met * 3.5 * weight / 200.0;
            }

            if (ee_kcal_min < 0) ee_kcal_min = 0;

            // Convert per-minute rate to actual burned calories for the elapsed time
            return (ee_kcal_min / 60.0) * elapsedSeconds;
        }
    }
}
