using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Tests
{
    public class DataContextTests : IDisposable
    {
        private readonly ApplicationDbContext _context;

        public DataContextTests()
        {
            _context = TestDbContextFactory.Create();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task CanAddAndRetrieveVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1, "Test Escape Room");

            // Act
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var retrieved = await _context.Venues.FindAsync(1);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Test Escape Room", retrieved.Name);
            Assert.Equal("Seattle", retrieved.City);
        }

        [Fact]
        public async Task CanAddRoomWithVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var room = TestDbContextFactory.CreateTestRoom(1, 1, "The Haunted House");

            // Act
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            // Assert
            var retrieved = await _context.Rooms
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.RoomId == 1);
            Assert.NotNull(retrieved);
            Assert.Equal("The Haunted House", retrieved.Name);
            Assert.Equal(1, retrieved.VenueId);
        }

        [Fact]
        public async Task CanAddReviewWithVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var review = TestDbContextFactory.CreateTestReview(1, 1, 5);

            // Act
            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Assert
            var retrieved = await _context.Reviews.FindAsync(1);
            Assert.NotNull(retrieved);
            Assert.Equal(5, retrieved.Rating);
            Assert.Equal(1, retrieved.VenueId);
        }

        [Fact]
        public async Task CanAddPhotoWithVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var photo = TestDbContextFactory.CreateTestPhoto(1, 1);

            // Act
            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            // Assert
            var retrieved = await _context.Photos.FindAsync(1);
            Assert.NotNull(retrieved);
            Assert.Equal(1, retrieved.VenueId);
        }

        [Fact]
        public async Task VenueRoomsNavigation_Works()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            _context.Rooms.Add(TestDbContextFactory.CreateTestRoom(1, 1, "Room A"));
            _context.Rooms.Add(TestDbContextFactory.CreateTestRoom(2, 1, "Room B"));
            await _context.SaveChangesAsync();

            // Act
            var venueWithRooms = await _context.Venues
                .Include(v => v.Rooms)
                .FirstOrDefaultAsync(v => v.VenueId == 1);

            // Assert
            Assert.NotNull(venueWithRooms);
            Assert.Equal(2, venueWithRooms.Rooms.Count);
        }

        [Fact]
        public async Task VenueReviewsNavigation_Works()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            _context.Reviews.Add(TestDbContextFactory.CreateTestReview(1, 1, 4));
            _context.Reviews.Add(TestDbContextFactory.CreateTestReview(2, 1, 5));
            _context.Reviews.Add(TestDbContextFactory.CreateTestReview(3, 1, 3));
            await _context.SaveChangesAsync();

            // Act
            var venueWithReviews = await _context.Venues
                .Include(v => v.Reviews)
                .FirstOrDefaultAsync(v => v.VenueId == 1);

            // Assert
            Assert.NotNull(venueWithReviews);
            Assert.Equal(3, venueWithReviews.Reviews.Count);
        }

        [Fact]
        public async Task DeleteVenue_RemovesFromDb()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            // Act
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();

            // Assert
            Assert.Equal(0, await _context.Venues.CountAsync());
        }

        [Fact]
        public async Task BulkDelete_RemovesAll()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 5);
            Assert.Equal(5, await _context.Venues.CountAsync());

            // Act
            _context.Reviews.RemoveRange(_context.Reviews);
            _context.Photos.RemoveRange(_context.Photos);
            _context.Rooms.RemoveRange(_context.Rooms);
            _context.Venues.RemoveRange(_context.Venues);
            await _context.SaveChangesAsync();

            // Assert
            Assert.Equal(0, await _context.Venues.CountAsync());
            Assert.Equal(0, await _context.Rooms.CountAsync());
            Assert.Equal(0, await _context.Reviews.CountAsync());
            Assert.Equal(0, await _context.Photos.CountAsync());
        }

        [Fact]
        public async Task PremiumListing_LinkedToVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            var listing = TestDbContextFactory.CreateTestPremiumListing(1, 1);
            _context.PremiumListings.Add(listing);
            await _context.SaveChangesAsync();

            // Act
            var venueWithListing = await _context.Venues
                .Include(v => v.PremiumListing)
                .FirstOrDefaultAsync(v => v.VenueId == 1);

            // Assert
            Assert.NotNull(venueWithListing);
            Assert.NotNull(venueWithListing.PremiumListing);
            Assert.Equal("Premium", venueWithListing.PremiumListing.PlanType);
        }

        [Fact]
        public async Task VenueSlug_MustBeUnique()
        {
            // Arrange
            var venue1 = TestDbContextFactory.CreateTestVenue(1);
            venue1.Slug = "same-slug";
            _context.Venues.Add(venue1);
            await _context.SaveChangesAsync();

            var venue2 = TestDbContextFactory.CreateTestVenue(2);
            venue2.Slug = "different-slug"; // Must be different

            // Act
            _context.Venues.Add(venue2);
            await _context.SaveChangesAsync();

            // Assert
            Assert.Equal(2, await _context.Venues.CountAsync());
        }

        [Fact]
        public async Task CanQueryVenuesByCity()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 6);

            // Act
            var seattleVenues = await _context.Venues
                .Where(v => v.City == "Seattle")
                .ToListAsync();

            var portlandVenues = await _context.Venues
                .Where(v => v.City == "Portland")
                .ToListAsync();

            // Assert
            Assert.True(seattleVenues.Count > 0);
            Assert.True(portlandVenues.Count > 0);
            Assert.Equal(6, seattleVenues.Count + portlandVenues.Count);
        }
    }
}
