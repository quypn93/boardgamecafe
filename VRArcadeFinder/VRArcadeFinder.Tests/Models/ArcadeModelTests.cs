using System.Text.Json;
using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Tests.Models
{
    public class ArcadeModelTests
    {
        #region GetVRPlatformsList Tests

        [Fact]
        public void GetVRPlatformsList_ReturnsParsedList()
        {
            var arcade = new Arcade { VRPlatforms = "Meta Quest, HTC Vive, PlayStation VR" };

            var result = arcade.GetVRPlatformsList();

            Assert.Equal(3, result.Count);
            Assert.Contains("Meta Quest", result);
            Assert.Contains("HTC Vive", result);
            Assert.Contains("PlayStation VR", result);
        }

        [Fact]
        public void GetVRPlatformsList_ReturnsEmptyForNull()
        {
            var arcade = new Arcade { VRPlatforms = null };

            var result = arcade.GetVRPlatformsList();

            Assert.Empty(result);
        }

        [Fact]
        public void GetVRPlatformsList_ReturnsEmptyForEmptyString()
        {
            var arcade = new Arcade { VRPlatforms = "" };

            var result = arcade.GetVRPlatformsList();

            Assert.Empty(result);
        }

        #endregion

        #region GetOpeningHours Tests

        [Fact]
        public void GetOpeningHours_ReturnsNull_WhenNoHours()
        {
            var arcade = new Arcade { OpeningHours = null };

            var result = arcade.GetOpeningHours();

            Assert.Null(result);
        }

        [Fact]
        public void GetOpeningHours_ReturnsNull_ForInvalidJson()
        {
            var arcade = new Arcade { OpeningHours = "not valid json" };

            var result = arcade.GetOpeningHours();

            Assert.Null(result);
        }

        [Fact]
        public void GetOpeningHours_ParsesValidJson()
        {
            var hours = new List<OpeningHourPeriod>
            {
                new OpeningHourPeriod { DayOfWeek = 1, OpenMinutes = 540, CloseMinutes = 1320 }
            };
            var arcade = new Arcade { OpeningHours = JsonSerializer.Serialize(hours) };

            var result = arcade.GetOpeningHours();

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(540, result[0].OpenMinutes);
        }

        #endregion

        #region GetAttributes / SetAttributes Tests

        [Fact]
        public void GetAttributes_ReturnsNull_WhenNoAttributes()
        {
            var arcade = new Arcade { AttributesJson = null };

            var result = arcade.GetAttributes();

            Assert.Null(result);
        }

        [Fact]
        public void SetAttributes_And_GetAttributes_RoundTrips()
        {
            var arcade = new Arcade();
            var attrs = new Dictionary<string, List<string>>
            {
                ["Amenities"] = new List<string> { "Parking", "Wi-Fi" },
                ["VR Equipment"] = new List<string> { "Quest 3", "Vive Pro 2" }
            };

            arcade.SetAttributes(attrs);
            var result = arcade.GetAttributes();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains("Parking", result["Amenities"]);
        }

        #endregion

        #region Default Values Tests

        [Fact]
        public void Arcade_HasCorrectDefaults()
        {
            var arcade = new Arcade();

            Assert.True(arcade.IsActive);
            Assert.False(arcade.IsVerified);
            Assert.False(arcade.IsPremium);
            Assert.False(arcade.IsFeatured);
            Assert.False(arcade.HasMultiplayerArea);
            Assert.False(arcade.HasPartyRooms);
            Assert.Equal(0, arcade.TotalReviews);
            Assert.Equal("United States", arcade.Country);
        }

        [Fact]
        public void Arcade_IdAlias_Works()
        {
            var arcade = new Arcade { ArcadeId = 42 };
            Assert.Equal(42, arcade.Id);
        }

        #endregion
    }

    public class OpeningHourPeriodTests
    {
        [Fact]
        public void GetDayName_ReturnsCorrectNames()
        {
            Assert.Equal("Sunday", new OpeningHourPeriod { DayOfWeek = 0 }.GetDayName());
            Assert.Equal("Monday", new OpeningHourPeriod { DayOfWeek = 1 }.GetDayName());
            Assert.Equal("Tuesday", new OpeningHourPeriod { DayOfWeek = 2 }.GetDayName());
            Assert.Equal("Wednesday", new OpeningHourPeriod { DayOfWeek = 3 }.GetDayName());
            Assert.Equal("Thursday", new OpeningHourPeriod { DayOfWeek = 4 }.GetDayName());
            Assert.Equal("Friday", new OpeningHourPeriod { DayOfWeek = 5 }.GetDayName());
            Assert.Equal("Saturday", new OpeningHourPeriod { DayOfWeek = 6 }.GetDayName());
            Assert.Equal("Unknown", new OpeningHourPeriod { DayOfWeek = 99 }.GetDayName());
        }

        [Theory]
        [InlineData(540, "9:00 AM")]
        [InlineData(780, "1:00 PM")]
        [InlineData(0, "12:00 AM")]
        [InlineData(720, "12:00 PM")]
        public void GetOpenTime_FormatsCorrectly(int minutes, string expected)
        {
            var period = new OpeningHourPeriod { OpenMinutes = minutes };
            Assert.Equal(expected, period.GetOpenTime());
        }

        [Theory]
        [InlineData(1320, "10:00 PM")]
        [InlineData(1080, "6:00 PM")]
        public void GetCloseTime_FormatsCorrectly(int minutes, string expected)
        {
            var period = new OpeningHourPeriod { CloseMinutes = minutes };
            Assert.Equal(expected, period.GetCloseTime());
        }
    }
}
