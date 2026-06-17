using System;
using System.Linq;
using Xunit;
using HeartRateMonitor;

namespace HeartRateMonitor.Tests
{
    public class SessionTests
    {
        [Fact]
        public void Session_Constructor_InitializesEmptyHistory()
        {
            // Act
            var session = new Session("Test Session");

            // Assert
            Assert.Equal("Test Session", session.Name);
            Assert.NotNull(session.History);
            Assert.Empty(session.History);
            Assert.Equal(0, session.AverageBpm);
            Assert.Equal(0, session.CaloriesBurned);
        }

        [Fact]
        public void MinMaxString_ReturnsCorrectFormat()
        {
            // Arrange
            var session = new Session();

            // Act & Assert (initial state)
            Assert.Equal("-- / --", session.MinMaxString);

            // Act & Assert (with values)
            session.MinBpm = 60;
            session.MaxBpm = 180;
            Assert.Equal("60 / 180", session.MinMaxString);
        }

        [Fact]
        public void StartTimeText_ReturnsCorrectFormat()
        {
            // Arrange
            var session = new Session();

            // Act & Assert (initial state)
            Assert.Equal("--", session.StartTimeText);

            // Act & Assert (with value)
            var startTime = new DateTime(2023, 10, 27, 14, 30, 0);
            session.StartTime = startTime;
            Assert.Equal("27.10.2023 14:30:00", session.StartTimeText);
        }

        [Fact]
        public void AvgBpmText_ReturnsCorrectFormat()
        {
            // Arrange
            var session = new Session();

            // Act & Assert (initial state)
            Assert.Equal("-- BPM", session.AvgBpmText);

            // Act & Assert (with value)
            session.AverageBpm = 125.6;
            Assert.Equal("126 BPM", session.AvgBpmText);
        }

        [Fact]
        public void CaloriesBurnedText_ReturnsCorrectFormat()
        {
            // Arrange
            var session = new Session();

            // Act & Assert (initial state)
            Assert.Equal("0 kcal", session.CaloriesBurnedText);

            // Act & Assert (with value)
            session.CaloriesBurned = 150.4;
            Assert.Equal("150 kcal", session.CaloriesBurnedText);
        }
    }
}
