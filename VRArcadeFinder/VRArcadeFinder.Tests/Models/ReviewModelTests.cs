using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Tests.Models
{
    public class ReviewModelTests
    {
        #region GetRatingStars Tests

        [Theory]
        [InlineData(1, "*")]
        [InlineData(3, "***")]
        [InlineData(5, "*****")]
        public void GetRatingStars_ReturnsCorrectStars(int rating, string expectedStars)
        {
            var review = new Review { Rating = rating };
            Assert.Equal(expectedStars, review.GetRatingStars());
        }

        #endregion

        #region GetTimeAgo Tests

        [Fact]
        public void GetTimeAgo_ReturnsJustNow_ForRecentReview()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddSeconds(-30)
            };

            Assert.Equal("just now", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsMinutesAgo()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            };

            Assert.Contains("minute", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsHoursAgo()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddHours(-5)
            };

            Assert.Contains("hour", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsDaysAgo()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            };

            Assert.Contains("day", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsMonthsAgo()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };

            Assert.Contains("month", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsYearsAgo()
        {
            var review = new Review
            {
                UserId = 1,
                Rating = 5,
                CreatedAt = DateTime.UtcNow.AddDays(-400)
            };

            Assert.Contains("year", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_UseVisitDate_ForCrawledReviews()
        {
            var review = new Review
            {
                UserId = null, // Crawled review
                Rating = 4,
                VisitDate = DateTime.UtcNow.AddDays(-5)
            };

            Assert.Contains("day", review.GetTimeAgo());
        }

        [Fact]
        public void GetTimeAgo_ReturnsEmpty_ForCrawledReviewWithNoDate()
        {
            var review = new Review
            {
                UserId = null,
                Rating = 4,
                VisitDate = null
            };

            Assert.Equal("", review.GetTimeAgo());
        }

        #endregion

        #region Default Values Tests

        [Fact]
        public void Review_HasCorrectDefaults()
        {
            var review = new Review();

            Assert.False(review.IsVerifiedVisit);
            Assert.False(review.IsApproved);
            Assert.Equal(0, review.HelpfulCount);
        }

        [Fact]
        public void Review_IdAlias_Works()
        {
            var review = new Review { ReviewId = 77 };
            Assert.Equal(77, review.Id);
        }

        #endregion
    }
}
