using EscapeRoomFinder.Models.Domain;

namespace EscapeRoomFinder.Tests
{
    public class EscapeRoomVenueTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var venue = new EscapeRoomVenue();

            Assert.Equal(string.Empty, venue.Name);
            Assert.Equal(string.Empty, venue.Address);
            Assert.Equal(string.Empty, venue.City);
            Assert.Equal("United States", venue.Country);
            Assert.True(venue.IsActive);
            Assert.False(venue.IsVerified);
            Assert.False(venue.IsPremium);
            Assert.Equal(0, venue.TotalRooms);
            Assert.Equal(0, venue.TotalReviews);
        }

        [Fact]
        public void IsOpenNow_NullOpeningHours_ReturnsFalse()
        {
            var venue = new EscapeRoomVenue { OpeningHours = null };
            Assert.False(venue.IsOpenNow());
        }

        [Fact]
        public void IsOpenNow_EmptyOpeningHours_ReturnsFalse()
        {
            var venue = new EscapeRoomVenue { OpeningHours = "" };
            Assert.False(venue.IsOpenNow());
        }

        [Fact]
        public void IsOpenNow_InvalidJson_ReturnsFalse()
        {
            var venue = new EscapeRoomVenue { OpeningHours = "not json" };
            Assert.False(venue.IsOpenNow());
        }

        [Fact]
        public void GetOpeningHours_NullValue_ReturnsNull()
        {
            var venue = new EscapeRoomVenue { OpeningHours = null };
            Assert.Null(venue.GetOpeningHours());
        }

        [Fact]
        public void GetOpeningHours_ValidJson_ReturnsParsedList()
        {
            var venue = new EscapeRoomVenue
            {
                OpeningHours = "[{\"DayOfWeek\":1,\"OpenMinutes\":540,\"CloseMinutes\":1320}]"
            };

            var hours = venue.GetOpeningHours();

            Assert.NotNull(hours);
            Assert.Single(hours);
            Assert.Equal(1, hours[0].DayOfWeek);
            Assert.Equal(540, hours[0].OpenMinutes);
            Assert.Equal(1320, hours[0].CloseMinutes);
        }

        [Fact]
        public void GetAttributes_NullValue_ReturnsNull()
        {
            var venue = new EscapeRoomVenue { AttributesJson = null };
            Assert.Null(venue.GetAttributes());
        }

        [Fact]
        public void SetAttributes_SerializesCorrectly()
        {
            var venue = new EscapeRoomVenue();
            var attrs = new Dictionary<string, List<string>>
            {
                { "parking", new List<string> { "Free parking", "Street parking" } },
                { "accessibility", new List<string> { "Wheelchair accessible" } }
            };

            venue.SetAttributes(attrs);

            Assert.NotNull(venue.AttributesJson);
            var parsed = venue.GetAttributes();
            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Count);
            Assert.Contains("parking", parsed.Keys);
        }

        [Fact]
        public void GetAverageSuccessRate_NoRooms_ReturnsZero()
        {
            var venue = new EscapeRoomVenue();
            Assert.Equal(0, venue.GetAverageSuccessRate());
        }

        [Fact]
        public void GetAverageSuccessRate_RoomsWithoutRate_ReturnsZero()
        {
            var venue = new EscapeRoomVenue();
            venue.Rooms.Add(new EscapeRoom { SuccessRate = null });
            Assert.Equal(0, venue.GetAverageSuccessRate());
        }

        [Fact]
        public void GetAverageSuccessRate_RoomsWithRates_ReturnsAverage()
        {
            var venue = new EscapeRoomVenue();
            venue.Rooms.Add(new EscapeRoom { SuccessRate = 60m });
            venue.Rooms.Add(new EscapeRoom { SuccessRate = 40m });

            var avg = venue.GetAverageSuccessRate();

            Assert.Equal(50.0, avg);
        }
    }

    public class EscapeRoomTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var room = new EscapeRoom();

            Assert.Equal("Mystery", room.Theme);
            Assert.Equal(3, room.Difficulty);
            Assert.Equal(2, room.MinPlayers);
            Assert.Equal(6, room.MaxPlayers);
            Assert.Equal(60, room.DurationMinutes);
            Assert.True(room.IsActive);
            Assert.True(room.IsKidFriendly);
            Assert.False(room.IsScaryOrIntense);
            Assert.False(room.HasActors);
            Assert.False(room.UsesVR);
        }

        [Theory]
        [InlineData(1, "Very Easy")]
        [InlineData(2, "Easy")]
        [InlineData(3, "Medium")]
        [InlineData(4, "Hard")]
        [InlineData(5, "Expert")]
        [InlineData(0, "Unknown")]
        [InlineData(6, "Unknown")]
        public void GetDifficultyText_ReturnsCorrectText(int difficulty, string expected)
        {
            var room = new EscapeRoom { Difficulty = difficulty };
            Assert.Equal(expected, room.GetDifficultyText());
        }

        [Theory]
        [InlineData(1, "bg-success")]
        [InlineData(2, "bg-info")]
        [InlineData(3, "bg-warning")]
        [InlineData(4, "bg-orange")]
        [InlineData(5, "bg-danger")]
        [InlineData(0, "bg-secondary")]
        public void GetDifficultyBadgeClass_ReturnsCorrectClass(int difficulty, string expected)
        {
            var room = new EscapeRoom { Difficulty = difficulty };
            Assert.Equal(expected, room.GetDifficultyBadgeClass());
        }

        [Fact]
        public void GetPlayerRange_SameMinMax_ReturnsSingular()
        {
            var room = new EscapeRoom { MinPlayers = 4, MaxPlayers = 4 };
            Assert.Equal("4 players", room.GetPlayerRange());
        }

        [Fact]
        public void GetPlayerRange_DifferentMinMax_ReturnsRange()
        {
            var room = new EscapeRoom { MinPlayers = 2, MaxPlayers = 8 };
            Assert.Equal("2-8 players", room.GetPlayerRange());
        }

        [Fact]
        public void GetPriceDisplay_PerPerson_ReturnsPerPerson()
        {
            var room = new EscapeRoom { PricePerPerson = 35m };
            Assert.Equal("$35/person", room.GetPriceDisplay());
        }

        [Fact]
        public void GetPriceDisplay_PerGroup_ReturnsPerGroup()
        {
            var room = new EscapeRoom { PricePerGroup = 200m };
            Assert.Equal("$200/group", room.GetPriceDisplay());
        }

        [Fact]
        public void GetPriceDisplay_NoPrice_ReturnsContactMessage()
        {
            var room = new EscapeRoom();
            Assert.Equal("Contact for pricing", room.GetPriceDisplay());
        }

        [Fact]
        public void GetPriceDisplay_BothPrices_PrefersPerPerson()
        {
            var room = new EscapeRoom { PricePerPerson = 30m, PricePerGroup = 180m };
            Assert.Equal("$30/person", room.GetPriceDisplay());
        }

        [Fact]
        public void GetSuccessRateDisplay_HasRate_ReturnsFormatted()
        {
            var room = new EscapeRoom { SuccessRate = 45.5m };
            Assert.Equal("46% escape rate", room.GetSuccessRateDisplay());
        }

        [Fact]
        public void GetSuccessRateDisplay_NoRate_ReturnsNoData()
        {
            var room = new EscapeRoom();
            Assert.Equal("No data", room.GetSuccessRateDisplay());
        }

        [Fact]
        public void GetFeatureTags_AllFalse_ReturnsOnlyDefaults()
        {
            var room = new EscapeRoom(); // IsKidFriendly = true by default
            var tags = room.GetFeatureTags();
            Assert.Contains("Kid Friendly", tags);
            Assert.DoesNotContain("Scary", tags);
            Assert.DoesNotContain("Live Actors", tags);
        }

        [Fact]
        public void GetFeatureTags_ScaryRoom_ReturnsScaryTags()
        {
            var room = new EscapeRoom
            {
                IsScaryOrIntense = true,
                HasJumpscares = true,
                HasActors = true,
                IsKidFriendly = false
            };

            var tags = room.GetFeatureTags();

            Assert.Contains("Scary", tags);
            Assert.Contains("Jump Scares", tags);
            Assert.Contains("Live Actors", tags);
            Assert.DoesNotContain("Kid Friendly", tags);
        }

        [Fact]
        public void GetFeatureTags_VRRoom_ReturnsVRTags()
        {
            var room = new EscapeRoom
            {
                UsesVR = true,
                HasHighTechPuzzles = true,
                IsKidFriendly = false
            };

            var tags = room.GetFeatureTags();

            Assert.Contains("VR Elements", tags);
            Assert.Contains("High-Tech", tags);
        }
    }

    public class ReviewTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var review = new Review();

            Assert.False(review.IsApproved);
            Assert.False(review.IsVerifiedVisit);
            Assert.Equal(0, review.HelpfulCount);
        }

        [Theory]
        [InlineData(1, "★☆☆☆☆")]
        [InlineData(2, "★★☆☆☆")]
        [InlineData(3, "★★★☆☆")]
        [InlineData(4, "★★★★☆")]
        [InlineData(5, "★★★★★")]
        public void GetRatingStars_ReturnsCorrectStars(int rating, string expected)
        {
            var review = new Review { Rating = rating };
            Assert.Equal(expected, review.GetRatingStars());
        }

        [Fact]
        public void GetTimeAgo_JustNow_ReturnsJustNow()
        {
            var review = new Review { CreatedAt = DateTime.UtcNow };
            Assert.Equal("just now", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_DaysAgo_ReturnsDays()
        {
            var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-5) };
            Assert.Contains("day", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_MonthsAgo_ReturnsMonths()
        {
            var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-60) };
            Assert.Contains("month", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_YearsAgo_ReturnsYears()
        {
            var review = new Review { CreatedAt = DateTime.UtcNow.AddDays(-400) };
            Assert.Contains("year", review.GetTimeAgo());
        }
    }

    public class OpeningHourPeriodTests
    {
        [Theory]
        [InlineData(0, "Sunday")]
        [InlineData(1, "Monday")]
        [InlineData(2, "Tuesday")]
        [InlineData(3, "Wednesday")]
        [InlineData(4, "Thursday")]
        [InlineData(5, "Friday")]
        [InlineData(6, "Saturday")]
        [InlineData(7, "Unknown")]
        public void GetDayName_ReturnsCorrectName(int day, string expected)
        {
            var period = new OpeningHourPeriod { DayOfWeek = day };
            Assert.Equal(expected, period.GetDayName());
        }

        [Theory]
        [InlineData(540, "9:00 AM")]   // 9:00 AM
        [InlineData(780, "1:00 PM")]   // 1:00 PM
        [InlineData(0, "12:00 AM")]    // Midnight
        [InlineData(720, "12:00 PM")]  // Noon
        [InlineData(930, "3:30 PM")]   // 3:30 PM
        public void GetOpenTime_ReturnsFormattedTime(int minutes, string expected)
        {
            var period = new OpeningHourPeriod { OpenMinutes = minutes };
            Assert.Equal(expected, period.GetOpenTime());
        }

        [Fact]
        public void GetCloseTime_ReturnsFormattedTime()
        {
            var period = new OpeningHourPeriod { CloseMinutes = 1320 }; // 10:00 PM
            Assert.Equal("10:00 PM", period.GetCloseTime());
        }
    }

    public class PremiumListingTests
    {
        [Fact]
        public void IsExpired_FutureEndDate_ReturnsFalse()
        {
            var listing = new PremiumListing { EndDate = DateTime.UtcNow.AddDays(30) };
            Assert.False(listing.IsExpired());
        }

        [Fact]
        public void IsExpired_PastEndDate_ReturnsTrue()
        {
            var listing = new PremiumListing { EndDate = DateTime.UtcNow.AddDays(-1) };
            Assert.True(listing.IsExpired());
        }

        [Fact]
        public void GetDaysRemaining_FutureDate_ReturnsPositive()
        {
            var listing = new PremiumListing { EndDate = DateTime.UtcNow.AddDays(15) };
            var days = listing.GetDaysRemaining();
            Assert.True(days >= 14 && days <= 15);
        }

        [Fact]
        public void GetDaysRemaining_PastDate_ReturnsZero()
        {
            var listing = new PremiumListing { EndDate = DateTime.UtcNow.AddDays(-5) };
            Assert.Equal(0, listing.GetDaysRemaining());
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var listing = new PremiumListing();
            Assert.Equal("Basic", listing.PlanType);
            Assert.True(listing.IsActive);
            Assert.False(listing.FeaturedPlacement);
            Assert.False(listing.BookingIntegration);
        }
    }
}
