using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Models.DTOs;
using EscapeRoomFinder.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace EscapeRoomFinder.Tests
{
    public class VenueServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly VenueService _service;
        private readonly Mock<ILogger<VenueService>> _loggerMock;

        public VenueServiceTests()
        {
            _context = TestDbContextFactory.Create();
            _loggerMock = new Mock<ILogger<VenueService>>();
            _service = new VenueService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ExistingVenue_ReturnsVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1, "Test Escape Room");
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Escape Room", result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingVenue_ReturnsNull()
        {
            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetBySlugAsync Tests

        [Fact]
        public async Task GetBySlugAsync_ExistingSlug_ReturnsVenue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1, "Test Venue");
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetBySlugAsync("test-venue-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Venue", result.Name);
        }

        [Fact]
        public async Task GetBySlugAsync_NonExistingSlug_ReturnsNull()
        {
            // Act
            var result = await _service.GetBySlugAsync("does-not-exist");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region SearchByTextAsync Tests

        [Fact]
        public async Task SearchByTextAsync_MatchingName_ReturnsResults()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var results = await _service.SearchByTextAsync("Escape Room 1");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, v => Assert.Contains("Escape Room 1", v.Name));
        }

        [Fact]
        public async Task SearchByTextAsync_MatchingCity_ReturnsResults()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var results = await _service.SearchByTextAsync("Seattle");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, v => Assert.Equal("Seattle", v.City));
        }

        [Fact]
        public async Task SearchByTextAsync_NoMatch_ReturnsEmpty()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var results = await _service.SearchByTextAsync("NonExistentCity");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchByTextAsync_RespectsLimit()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 10);

            // Act
            var results = await _service.SearchByTextAsync("Escape", limit: 3);

            // Assert
            Assert.True(results.Count <= 3);
        }

        [Fact]
        public async Task SearchByTextAsync_OnlyReturnsActive()
        {
            // Arrange
            var activeVenue = TestDbContextFactory.CreateTestVenue(1, "Active Escape");
            activeVenue.IsActive = true;
            _context.Venues.Add(activeVenue);

            var inactiveVenue = TestDbContextFactory.CreateTestVenue(2, "Inactive Escape");
            inactiveVenue.IsActive = false;
            _context.Venues.Add(inactiveVenue);
            await _context.SaveChangesAsync();

            // Act
            var results = await _service.SearchByTextAsync("Escape");

            // Assert
            Assert.Single(results);
            Assert.Equal("Active Escape", results[0].Name);
        }

        #endregion

        #region CreateVenueAsync Tests

        [Fact]
        public async Task CreateVenueAsync_ValidVenue_ReturnsCreated()
        {
            // Arrange
            var venue = new EscapeRoomVenue
            {
                Name = "New Venue",
                Address = "456 Main St",
                City = "Portland",
                Country = "United States",
                Latitude = 45.5152,
                Longitude = -122.6784,
                Slug = "new-venue"
            };

            // Act
            var result = await _service.CreateVenueAsync(venue);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.VenueId > 0);
            Assert.Equal("New Venue", result.Name);
        }

        [Fact]
        public async Task CreateVenueAsync_SavesToDB()
        {
            // Arrange
            var venue = new EscapeRoomVenue
            {
                Name = "Persisted Venue",
                Address = "789 Broadway",
                City = "Chicago",
                Country = "United States",
                Latitude = 41.8781,
                Longitude = -87.6298,
                Slug = "persisted-venue"
            };

            // Act
            await _service.CreateVenueAsync(venue);

            // Assert
            var dbVenue = await _context.Venues.FirstOrDefaultAsync(v => v.Name == "Persisted Venue");
            Assert.NotNull(dbVenue);
            Assert.Equal("Chicago", dbVenue.City);
        }

        #endregion

        #region DeleteVenueAsync Tests

        [Fact]
        public async Task DeleteVenueAsync_ExistingVenue_ReturnsTrue()
        {
            // Arrange
            var venue = TestDbContextFactory.CreateTestVenue(1);
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.DeleteVenueAsync(1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteVenueAsync_NonExistingVenue_ReturnsFalse()
        {
            // Act
            var result = await _service.DeleteVenueAsync(999);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetAllCitiesAsync Tests

        [Fact]
        public async Task GetAllCitiesAsync_ReturnsDistinctCities()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 4);

            // Act
            var cities = await _service.GetAllCitiesAsync();

            // Assert
            Assert.NotNull(cities);
            Assert.True(cities.Count > 0);
            Assert.Equal(cities.Count, cities.Distinct().Count()); // All unique
        }

        [Fact]
        public async Task GetAllCitiesAsync_EmptyDb_ReturnsEmpty()
        {
            // Act
            var cities = await _service.GetAllCitiesAsync();

            // Assert
            Assert.Empty(cities);
        }

        #endregion

        #region GetTotalVenueCountAsync / GetTotalRoomCountAsync

        [Fact]
        public async Task GetTotalVenueCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 5);

            // Act
            var count = await _service.GetTotalVenueCountAsync();

            // Assert
            Assert.Equal(5, count);
        }

        [Fact]
        public async Task GetTotalRoomCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var count = await _service.GetTotalRoomCountAsync();

            // Assert
            Assert.Equal(6, count); // 2 rooms per venue
        }

        #endregion

        #region FilterVenuesAsync Tests

        [Fact]
        public async Task FilterVenuesAsync_ByCity_FiltersCorrectly()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 4);

            // Act
            var results = await _service.FilterVenuesAsync(null, "Seattle", null, null, null, null);

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, v => Assert.Equal("Seattle", v.City));
        }

        [Fact]
        public async Task FilterVenuesAsync_ByCountry_FiltersCorrectly()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var results = await _service.FilterVenuesAsync("United States", null, null, null, null, null);

            // Assert
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task FilterVenuesAsync_NoFilters_ReturnsAll()
        {
            // Arrange
            await TestDbContextFactory.SeedTestDataAsync(_context, 3);

            // Act
            var results = await _service.FilterVenuesAsync(null, null, null, null, null, null);

            // Assert
            Assert.Equal(3, results.Count);
        }

        #endregion
    }
}
