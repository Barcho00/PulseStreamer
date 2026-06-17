using System;
using System.Windows.Media;

namespace HeartRateMonitor
{
    public static class FitnessCalculator
    {
        /// <summary>
        /// Tanaka formula (2001, JACC) — more accurate than the classic 220-age.
        /// </summary>
        public static double CalculateMaxHeartRate(int age)
        {
            return 208.0 - (0.7 * age);
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

        /// <summary>
        /// Returns the maximum MET for a given activity type.
        /// Values based on the Compendium of Physical Activities (2024 update).
        /// </summary>
        private static double GetMaxMET(string activity)
        {
            return activity switch
            {
                "Aerobik"    => 7.3,   // High-impact aerobics
                "VR Fitness" => 7.0,   // Intensive VR (Beat Saber Expert+, FitXR)
                "Bieżnia"    => 9.8,   // Running ~6 min/km pace
                "Boks"       => 10.5,  // Heavy bag / sparring
                "Rower"      => 8.5,   // Vigorous stationary cycling
                _            => 6.0    // General moderate exercise
            };
        }

        /// <summary>
        /// Returns the minimum (resting/light) MET for a given activity type.
        /// </summary>
        private static double GetMinMET(string activity)
        {
            return activity switch
            {
                "Aerobik"    => 4.0,   // Low-impact aerobics
                "VR Fitness" => 3.0,   // Light VR movement
                "Bieżnia"    => 3.5,   // Walking slowly
                "Boks"       => 4.0,   // Shadow boxing, light
                "Rower"      => 3.5,   // Light stationary cycling
                _            => 3.0    // Light general exercise
            };
        }

        /// <summary>
        /// Calculates the activity correction factor for Keytel formula.
        /// These factors adjust for the relationship between HR and energy expenditure
        /// varying by exercise modality (e.g. cycling uses less upper body, so HR 
        /// underestimates energy cost less than running).
        /// </summary>
        private static double GetKeytelActivityFactor(string activity)
        {
            return activity switch
            {
                "Aerobik"    => 1.00,  // Reference activity — mixed full-body
                "VR Fitness" => 0.90,  // Upper-body dominant, moderate load
                "Bieżnia"    => 1.05,  // Running — slight HR-efficiency advantage
                "Boks"       => 1.12,  // Heavy intermittent upper body + core
                "Rower"      => 0.90,  // Lower-body dominant, seated — HR overestimates EE
                _            => 1.00   // Default
            };
        }

        /// <summary>
        /// Improved calorie calculation using:
        /// - Keytel formula (HR >= 85 BPM) with activity-specific correction factors
        /// - Dynamic MET based on Heart Rate Reserve (HR < 85 BPM or fallback)
        /// 
        /// The dynamic MET method scales between min and max MET values for the activity
        /// based on the user's %HRR (Heart Rate Reserve), providing intensity-aware
        /// calorie estimation even at low heart rates.
        /// </summary>
        public static double CalculateCaloriesBurned(int bpm, double weight, double age, bool isMale, 
            string activity, double elapsedSeconds, int restingHR = 70)
        {
            if (elapsedSeconds <= 0) return 0;

            double maxHR = CalculateMaxHeartRate((int)age);
            double ee_kcal_min;

            if (bpm >= 85)
            {
                // Keytel Formula (Keytel et al. 2005, EJAP)
                if (isMale)
                {
                    ee_kcal_min = (-55.0969 + 0.6309 * bpm + 0.1988 * weight + 0.2017 * age) / 4.184;
                }
                else
                {
                    ee_kcal_min = (-20.4022 + 0.4472 * bpm - 0.1263 * weight + 0.074 * age) / 4.184;
                }

                // Apply activity-specific correction factor
                ee_kcal_min *= GetKeytelActivityFactor(activity);
            }
            else
            {
                // Dynamic MET based on Heart Rate Reserve (%HRR)
                // This scales MET between min and max values for the activity
                // based on how hard the user is working relative to their capacity
                double hrReserve = maxHR - restingHR;
                double hrr = hrReserve > 0 ? Math.Max(0, (bpm - restingHR)) / hrReserve : 0;
                hrr = Math.Min(hrr, 1.0); // clamp to [0, 1]

                double minMET = GetMinMET(activity);
                double maxMET = GetMaxMET(activity);
                double dynamicMET = minMET + (hrr * (maxMET - minMET));

                ee_kcal_min = dynamicMET * 3.5 * weight / 200.0;
            }

            if (ee_kcal_min < 0) ee_kcal_min = 0;

            // Convert per-minute rate to actual burned calories for the elapsed time
            return (ee_kcal_min / 60.0) * elapsedSeconds;
        }

        /// <summary>
        /// Estimates EPOC (Excess Post-Exercise Oxygen Consumption) — the "afterburn" effect.
        /// Higher intensity workouts cause the body to continue burning calories after exercise.
        /// Based on Børsheim & Bahr (2003), Sports Medicine.
        /// </summary>
        /// <param name="totalCalories">Total calories burned during the workout</param>
        /// <param name="avgBpm">Average BPM during the workout</param>
        /// <param name="age">User's age</param>
        /// <param name="durationMinutes">Workout duration in minutes</param>
        /// <returns>Estimated EPOC in kcal</returns>
        public static double EstimateEPOC(double totalCalories, double avgBpm, int age, double durationMinutes)
        {
            if (totalCalories <= 0 || avgBpm <= 0 || durationMinutes <= 0) return 0;

            double maxHR = CalculateMaxHeartRate(age);
            double avgIntensity = avgBpm / maxHR; // % of max HR

            // EPOC scaling based on average intensity and duration
            // High intensity (>85% max HR) + longer duration = more EPOC
            double intensityFactor;
            if (avgIntensity >= 0.85)
                intensityFactor = 0.15; // 15% — HIIT / anaerobic threshold
            else if (avgIntensity >= 0.75)
                intensityFactor = 0.10; // 10% — vigorous cardio
            else if (avgIntensity >= 0.65)
                intensityFactor = 0.06; // 6% — moderate cardio
            else
                intensityFactor = 0.03; // 3% — light activity

            // Duration modifier: longer workouts generate more EPOC (diminishing returns)
            double durationModifier = Math.Min(durationMinutes / 30.0, 2.0); // max 2x at 60+ min

            return totalCalories * intensityFactor * durationModifier;
        }
    }
}
