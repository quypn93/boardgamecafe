using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Tests
{
    public static class TestDbContextFactory
    {
        public static ApplicationDbContext Create(string? dbName = null)
        {
            dbName ??= Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        public static EscapeRoomVenue CreateTestVenue(int id = 1, string name = "Test Venue", string city = "Seattle")
        {
            return new EscapeRoomVenue
            {
                VenueId = id,
                Name = name,
                Address = "123 Test St",
                City = city,
                State = "WA",
                Country = "United States",
                Latitude = 47.6062,
                Longitude = -122.3321,
                Slug = $"test-venue-{id}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static EscapeRoom CreateTestRoom(int id = 1, int venueId = 1, string name = "The Mystery Room")
        {
            return new EscapeRoom
            {
                RoomId = id,
                VenueId = venueId,
                Name = name,
                Theme = "Mystery",
                Difficulty = 3,
                MinPlayers = 2,
                MaxPlayers = 6,
                DurationMinutes = 60,
                Slug = $"test-room-{id}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Review CreateTestReview(int id = 1, int venueId = 1, int rating = 4)
        {
            return new Review
            {
                ReviewId = id,
                VenueId = venueId,
                Rating = rating,
                Title = "Great experience",
                Content = "Had a wonderful time!",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static Photo CreateTestPhoto(int id = 1, int venueId = 1)
        {
            return new Photo
            {
                PhotoId = id,
                VenueId = venueId,
                Url = "https://example.com/photo.jpg",
                LocalPath = "/images/venues/test.jpg",
                IsApproved = true,
                UploadedAt = DateTime.UtcNow
            };
        }

        public static PremiumListing CreateTestPremiumListing(int id = 1, int venueId = 1)
        {
            return new PremiumListing
            {
                ListingId = id,
                VenueId = venueId,
                PlanType = "Premium",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                MonthlyFee = 100m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Seeds a complete set of test data into the context
        /// </summary>
        public static async Task SeedTestDataAsync(ApplicationDbContext context, int venueCount = 3)
        {
            for (int i = 1; i <= venueCount; i++)
            {
                var venue = CreateTestVenue(i, $"Escape Room {i}", i % 2 == 0 ? "Portland" : "Seattle");
                context.Venues.Add(venue);
            }
            await context.SaveChangesAsync();

            // Add rooms for each venue
            int roomId = 1;
            foreach (var venue in context.Venues.ToList())
            {
                for (int j = 0; j < 2; j++)
                {
                    context.Rooms.Add(CreateTestRoom(roomId++, venue.VenueId, $"Room {roomId} at {venue.Name}"));
                }
            }
            await context.SaveChangesAsync();

            // Add reviews and photos
            int reviewId = 1;
            int photoId = 1;
            foreach (var venue in context.Venues.ToList())
            {
                context.Reviews.Add(CreateTestReview(reviewId++, venue.VenueId, 4));
                context.Reviews.Add(CreateTestReview(reviewId++, venue.VenueId, 5));
                context.Photos.Add(CreateTestPhoto(photoId++, venue.VenueId));
            }
            await context.SaveChangesAsync();
        }
    }
}
