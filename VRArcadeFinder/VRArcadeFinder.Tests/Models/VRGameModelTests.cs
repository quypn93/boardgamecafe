using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Tests.Models
{
    public class VRGameModelTests
    {
        #region GetPlayerRange Tests

        [Fact]
        public void GetPlayerRange_ReturnsUnknown_WhenBothNull()
        {
            var game = new VRGame { MinPlayers = null, MaxPlayers = null };
            Assert.Equal("Unknown", game.GetPlayerRange());
        }

        [Fact]
        public void GetPlayerRange_ShowsSingleValue_WhenEqual()
        {
            var game = new VRGame { MinPlayers = 2, MaxPlayers = 2 };
            Assert.Equal("2 players", game.GetPlayerRange());
        }

        [Fact]
        public void GetPlayerRange_ShowsRange()
        {
            var game = new VRGame { MinPlayers = 1, MaxPlayers = 4 };
            Assert.Equal("1-4 players", game.GetPlayerRange());
        }

        [Fact]
        public void GetPlayerRange_ShowsPlus_WhenMaxNull()
        {
            var game = new VRGame { MinPlayers = 2, MaxPlayers = null };
            Assert.Equal("2-+ players", game.GetPlayerRange());
        }

        #endregion

        #region GetPlaytime Tests

        [Fact]
        public void GetPlaytime_ReturnsUnknown_WhenNull()
        {
            var game = new VRGame { PlaytimeMinutes = null };
            Assert.Equal("Unknown", game.GetPlaytime());
        }

        [Fact]
        public void GetPlaytime_ShowsMinutes_WhenUnderHour()
        {
            var game = new VRGame { PlaytimeMinutes = 45 };
            Assert.Equal("45 min", game.GetPlaytime());
        }

        [Fact]
        public void GetPlaytime_ShowsHours_WhenExactHour()
        {
            var game = new VRGame { PlaytimeMinutes = 120 };
            Assert.Equal("2h", game.GetPlaytime());
        }

        [Fact]
        public void GetPlaytime_ShowsHoursAndMinutes()
        {
            var game = new VRGame { PlaytimeMinutes = 90 };
            Assert.Equal("1h 30m", game.GetPlaytime());
        }

        #endregion

        #region GetIntensityBadgeClass Tests

        [Theory]
        [InlineData("Low", "bg-success")]
        [InlineData("Medium", "bg-warning")]
        [InlineData("High", "bg-orange")]
        [InlineData("Extreme", "bg-danger")]
        [InlineData(null, "bg-secondary")]
        [InlineData("Unknown", "bg-secondary")]
        public void GetIntensityBadgeClass_ReturnsCorrectClass(string? intensity, string expectedClass)
        {
            var game = new VRGame { IntensityLevel = intensity };
            Assert.Equal(expectedClass, game.GetIntensityBadgeClass());
        }

        #endregion

        #region Default Values Tests

        [Fact]
        public void VRGame_HasCorrectDefaults()
        {
            var game = new VRGame();

            Assert.False(game.RequiresRoomScale);
            Assert.False(game.IsMultiplayer);
            Assert.False(game.IsCoOp);
            Assert.Equal(string.Empty, game.Name);
        }

        [Fact]
        public void VRGame_IdAlias_Works()
        {
            var game = new VRGame { GameId = 99 };
            Assert.Equal(99, game.Id);
        }

        #endregion
    }
}
