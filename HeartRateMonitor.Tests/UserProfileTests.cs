using System;
using Xunit;
using HeartRateMonitor;

namespace HeartRateMonitor.Tests
{
    public class UserProfileTests
    {
        [Fact]
        public void UserProfile_Constructor_SetsDefaults()
        {
            // Act
            var profile = new UserProfile();

            // Assert
            Assert.NotEqual(Guid.Empty, profile.Id);
            Assert.Equal("Użytkownik", profile.Name);
            Assert.Equal(30, profile.Age);
            Assert.Equal(75.0, profile.Weight);
            Assert.Equal("Male", profile.Gender);
            Assert.Equal("Bieżnia", profile.PreferredActivity);
            
            // Default Training Goal
            Assert.Equal("Brak", profile.GoalType);
            Assert.Equal(45, profile.TargetDurationMinutes);
            Assert.Equal(500, profile.TargetCalories);

            // Default Zone Guard
            Assert.False(profile.ZoneGuardEnabled);
            Assert.Equal(3, profile.ZoneGuardTargetZone);
            Assert.Equal(30, profile.ZoneGuardCheckIntervalSeconds);
        }

    }
}
