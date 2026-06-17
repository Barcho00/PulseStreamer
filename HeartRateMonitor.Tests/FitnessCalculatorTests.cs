using System;
using System.Windows.Media;
using Xunit;
using HeartRateMonitor;

namespace HeartRateMonitor.Tests
{
    public class FitnessCalculatorTests
    {
        [Fact]
        public void CalculateMaxHeartRate_ShouldUseTanakaFormula()
        {
            // Arrange
            int age = 30;

            // Act — Tanaka formula: 208 - 0.7 * age
            double maxHr = FitnessCalculator.CalculateMaxHeartRate(age);

            // Assert
            Assert.Equal(187.0, maxHr);
        }

        [Theory]
        [InlineData(187, 30, 5)] // 100% max HR
        [InlineData(169, 30, 5)] // ~90% max HR (169/187 = 0.904)
        [InlineData(150, 30, 4)] // ~80% max HR (150/187 = 0.802)
        [InlineData(131, 30, 3)] // ~70% max HR (131/187 = 0.700)
        [InlineData(113, 30, 2)] // ~60% max HR (113/187 = 0.604)
        [InlineData(94, 30, 1)]  // ~50% max HR (94/187 = 0.503)
        [InlineData(80, 30, 0)]  // <50% max HR
        public void GetHeartRateZone_ShouldReturnCorrectZone(int bpm, int age, int expectedZone)
        {
            // Act
            int zone = FitnessCalculator.GetHeartRateZone(bpm, age);

            // Assert
            Assert.Equal(expectedZone, zone);
        }

        [Fact]
        public void GetZoneName_ShouldReturnCorrectName()
        {
            // Act & Assert
            Assert.Equal("Rozgrzewka", FitnessCalculator.GetZoneName(1));
            Assert.Equal("Spalanie Tłuszczu", FitnessCalculator.GetZoneName(2));
            Assert.Equal("Cardio / Aerobowa", FitnessCalculator.GetZoneName(3));
            Assert.Equal("Próg Beztlenowy", FitnessCalculator.GetZoneName(4));
            Assert.Equal("Ekstremalny Wysiłek", FitnessCalculator.GetZoneName(5));
            Assert.Equal("Odpoczynek", FitnessCalculator.GetZoneName(0));
            Assert.Equal("Odpoczynek", FitnessCalculator.GetZoneName(-1));
        }

        [Fact]
        public void GetZoneLimits_ShouldReturnCorrectLimits()
        {
            // Arrange
            int age = 30; // Max HR = 208 - 0.7*30 = 187

            // Act & Assert
            Assert.Equal((94, 112), FitnessCalculator.GetZoneLimits(1, age)); // 50-60%
            Assert.Equal((112, 131), FitnessCalculator.GetZoneLimits(2, age)); // 60-70%
            Assert.Equal((131, 150), FitnessCalculator.GetZoneLimits(3, age)); // 70-80%
            Assert.Equal((150, 168), FitnessCalculator.GetZoneLimits(4, age)); // 80-90%
            Assert.Equal((168, 187), FitnessCalculator.GetZoneLimits(5, age)); // 90-100%
        }

        [Fact]
        public void CalculateCaloriesBurned_Below85Bpm_UsesDynamicMET()
        {
            // Arrange
            int bpm = 80;
            double weight = 80.0;
            double age = 30.0;
            bool isMale = true;
            string activity = "Bieżnia"; // MinMET=3.5, MaxMET=9.8
            double elapsedSeconds = 60.0;
            int restingHR = 60;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds, restingHR);

            // Assert
            // MaxHR = 208 - 0.7*30 = 187
            // HRR = (80 - 60) / (187 - 60) = 20/127 = 0.1575
            // dynamicMET = 3.5 + (0.1575 * (9.8 - 3.5)) = 3.5 + 0.992 = 4.492
            // kcal/min = 4.492 * 3.5 * 80 / 200 = 6.289
            // For 60s = 6.289 kcal
            Assert.True(calories > 5.0 && calories < 8.0, $"Expected ~6.3 kcal but got {calories:F2}");
        }

        [Fact]
        public void CalculateCaloriesBurned_Above85Bpm_UsesKeytelFormulaMale()
        {
            // Arrange
            int bpm = 120;
            double weight = 80.0;
            double age = 30.0;
            bool isMale = true;
            string activity = "Bieżnia"; // Keytel factor = 1.05
            double elapsedSeconds = 60.0;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds);

            // Assert
            // (-55.0969 + 0.6309*120 + 0.1988*80 + 0.2017*30) / 4.184
            // = (-55.0969 + 75.708 + 15.904 + 6.051) / 4.184 = 42.5661 / 4.184 = 10.1735 kcal/min
            // Activity factor = 1.05 -> 10.1735 * 1.05 = 10.68 kcal
            Assert.Equal(10.68, calories, 1);
        }

        [Fact]
        public void CalculateCaloriesBurned_Above85Bpm_UsesKeytelFormulaFemale()
        {
            // Arrange
            int bpm = 120;
            double weight = 60.0;
            double age = 30.0;
            bool isMale = false;
            string activity = "Rower"; // Keytel factor = 0.90
            double elapsedSeconds = 60.0;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds);

            // Assert
            // (-20.4022 + 0.4472*120 - 0.1263*60 + 0.074*30) / 4.184
            // = (-20.4022 + 53.664 - 7.578 + 2.22) / 4.184 = 27.9038 / 4.184 = 6.669 kcal/min
            // Activity factor = 0.90 -> 6.669 * 0.90 = 6.002 kcal
            Assert.Equal(6.00, calories, 1);
        }

        [Fact]
        public void CalculateCaloriesBurned_DynamicMET_IntensityAware()
        {
            // Two workouts with same BPM<85 but different activities should yield different calories
            int bpm = 75;
            double weight = 80.0;
            double age = 30.0;
            bool isMale = true;
            double elapsedSeconds = 60.0;
            int restingHR = 60;

            double calBieznia = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, "Bieżnia", elapsedSeconds, restingHR);
            double calVR = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, "VR Fitness", elapsedSeconds, restingHR);

            // Bieżnia has higher MET range than VR Fitness
            Assert.True(calBieznia > calVR, $"Bieżnia ({calBieznia:F2}) should burn more than VR ({calVR:F2}) at same HR");
        }

        [Fact]
        public void CalculateCaloriesBurned_ZeroElapsedTime_ReturnsZero()
        {
            double calories = FitnessCalculator.CalculateCaloriesBurned(120, 80, 30, true, "Bieżnia", 0);
            Assert.Equal(0, calories);
        }

        [Fact]
        public void CalculateCaloriesBurned_NegativeElapsedTime_ReturnsZero()
        {
            double calories = FitnessCalculator.CalculateCaloriesBurned(120, 80, 30, true, "Bieżnia", -10);
            Assert.Equal(0, calories);
        }

        // --- EPOC Tests ---

        [Fact]
        public void EstimateEPOC_HighIntensity_ReturnsSignificantValue()
        {
            // Arrange — high intensity workout
            double totalCalories = 500;
            double avgBpm = 170; // ~90% of max HR for age 30 (187)
            int age = 30;
            double durationMinutes = 45;

            // Act
            double epoc = FitnessCalculator.EstimateEPOC(totalCalories, avgBpm, age, durationMinutes);

            // Assert — should be 15% * min(45/30, 2) = 15% * 1.5 = 22.5% of 500 = 112.5
            Assert.True(epoc > 100 && epoc < 130, $"Expected ~112 kcal EPOC but got {epoc:F2}");
        }

        [Fact]
        public void EstimateEPOC_LowIntensity_ReturnsSmallValue()
        {
            // Arrange — light workout
            double totalCalories = 200;
            double avgBpm = 110; // ~59% of max HR for age 30 (187)
            int age = 30;
            double durationMinutes = 30;

            // Act
            double epoc = FitnessCalculator.EstimateEPOC(totalCalories, avgBpm, age, durationMinutes);

            // Assert — should be 3% * 1.0 = 6 kcal
            Assert.Equal(6.0, epoc, 1);
        }

        [Fact]
        public void EstimateEPOC_ZeroCalories_ReturnsZero()
        {
            double epoc = FitnessCalculator.EstimateEPOC(0, 150, 30, 30);
            Assert.Equal(0, epoc);
        }

        [Fact]
        public void EstimateEPOC_ZeroDuration_ReturnsZero()
        {
            double epoc = FitnessCalculator.EstimateEPOC(300, 150, 30, 0);
            Assert.Equal(0, epoc);
        }
    }
}
