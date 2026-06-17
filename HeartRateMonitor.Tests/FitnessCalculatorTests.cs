using System;
using System.Windows.Media;
using Xunit;
using HeartRateMonitor;

namespace HeartRateMonitor.Tests
{
    public class FitnessCalculatorTests
    {
        [Fact]
        public void CalculateMaxHeartRate_ShouldReturnCorrectValue()
        {
            // Arrange
            int age = 30;

            // Act
            double maxHr = FitnessCalculator.CalculateMaxHeartRate(age);

            // Assert
            Assert.Equal(190.0, maxHr);
        }

        [Theory]
        [InlineData(190, 30, 5)] // 100% max HR
        [InlineData(171, 30, 5)] // 90% max HR
        [InlineData(152, 30, 4)] // 80% max HR
        [InlineData(133, 30, 3)] // 70% max HR
        [InlineData(114, 30, 2)] // 60% max HR
        [InlineData(95, 30, 1)]  // 50% max HR
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
            int age = 30; // Max HR = 190

            // Act & Assert
            Assert.Equal((95, 114), FitnessCalculator.GetZoneLimits(1, age)); // 50-60%
            Assert.Equal((114, 133), FitnessCalculator.GetZoneLimits(2, age)); // 60-70%
            Assert.Equal((133, 152), FitnessCalculator.GetZoneLimits(3, age)); // 70-80%
            Assert.Equal((152, 171), FitnessCalculator.GetZoneLimits(4, age)); // 80-90%
            Assert.Equal((171, 190), FitnessCalculator.GetZoneLimits(5, age)); // 90-100%
        }

        [Fact]
        public void CalculateCaloriesBurned_Below85Bpm_UsesMetFormula()
        {
            // Arrange
            int bpm = 80;
            double weight = 80.0;
            double age = 30.0;
            bool isMale = true;
            string activity = "Bieżnia"; // MET = 8.0
            double elapsedSeconds = 60.0;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds);

            // Assert
            // MET Formula: 8.0 * 3.5 * 80 / 200 = 11.2 kcal/min
            // Elapsed 60s = 1 min
            Assert.Equal(11.2, calories, 2);
        }

        [Fact]
        public void CalculateCaloriesBurned_Above85Bpm_UsesKeytelFormulaMale()
        {
            // Arrange
            int bpm = 120;
            double weight = 80.0;
            double age = 30.0;
            bool isMale = true;
            string activity = "Bieżnia"; // factor = 1.05
            double elapsedSeconds = 60.0;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds);

            // Assert
            // (-55.0969 + 0.6309*120 + 0.1988*80 + 0.2017*30) / 4.184
            // = (-55.0969 + 75.708 + 15.904 + 6.051) / 4.184 = 42.5661 / 4.184 = 10.1735 kcal/min
            // Activity factor = 1.05 -> 10.1735 * 1.05 = 10.68 kcal
            Assert.Equal(10.68, calories, 2);
        }

        [Fact]
        public void CalculateCaloriesBurned_Above85Bpm_UsesKeytelFormulaFemale()
        {
            // Arrange
            int bpm = 120;
            double weight = 60.0;
            double age = 30.0;
            bool isMale = false;
            string activity = "Rower"; // factor = 0.95
            double elapsedSeconds = 60.0;

            // Act
            double calories = FitnessCalculator.CalculateCaloriesBurned(bpm, weight, age, isMale, activity, elapsedSeconds);

            // Assert
            // (-20.4022 + 0.4472*120 - 0.1263*60 + 0.074*30) / 4.184
            // = (-20.4022 + 53.664 - 7.578 + 2.22) / 4.184 = 27.9038 / 4.184 = 6.669 kcal/min
            // Activity factor = 0.95 -> 6.669 * 0.95 = 6.33 kcal
            Assert.Equal(6.34, calories, 2);
        }
    }
}
