using Microsoft.Extensions.Logging;
using Moq;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Models.DTOs;
using VRArcadeFinder.Services;
using VRArcadeFinder.Tests.Helpers;

namespace VRArcadeFinder.Tests.Services
{
    public class ArcadeServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ArcadeService _service;
        private readonly Mock<ILogger<ArcadeService>> _loggerMock;

        public ArcadeServiceTests()
        {
            _context = TestDbContextFactory.Create();
            _loggerMock = new Mock<ILogger<ArcadeService>>();
            _service = new ArcadeService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region SearchNearbyAsync Tests

        [Fact]
        public async Task SearchNearbyAsync_ReturnsArcadesWithinRadius()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 50000, // 50km
                Limit = 50
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.True(r.Distance <= request.Radius));
        }

        [Fact]
        public async Task SearchNearbyAsync_ReturnsOrderedByDistance()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 100000,
                Limit = 50
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            if (results.Count > 1)
            {
                for (int i = 1; i < results.Count; i++)
                {
                    Assert.True(results[i].Distance >= results[i - 1].Distance,
                        "Results should be ordered by distance ascending");
                }
            }
        }

        [Fact]
        public async Task SearchNearbyAsync_RespectsLimitParameter()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context, 10);
            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 100000,
                Limit = 3
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.True(results.Count <= 3);
        }

        [Fact]
        public async Task SearchNearbyAsync_FiltersInactiveArcades()
        {
            // Arrange
            _context.Arcades.Add(new Arcade
            {
                Name = "Inactive VR",
                Address = "123 St",
                City = "NY",
                Slug = "inactive-vr",
                Latitude = 40.7128,
                Longitude = -74.0060,
                IsActive = false
            });
            _context.Arcades.Add(new Arcade
            {
                Name = "Active VR",
                Address = "456 St",
                City = "NY",
                Slug = "active-vr",
                Latitude = 40.7130,
                Longitude = -74.0058,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 50000,
                Limit = 50
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.Single(results);
            Assert.Equal("Active VR", results[0].Name);
        }

        [Fact]
        public async Task SearchNearbyAsync_FiltersByMinRating()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 100000,
                Limit = 50,
                MinRating = 4.0m
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.All(results, r => Assert.True(r.AverageRating >= 4.0m));
        }

        [Fact]
        public async Task SearchNearbyAsync_FiltersByVRPlatform()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var request = new ArcadeSearchRequest
            {
                Latitude = 40.7128,
                Longitude = -74.0060,
                Radius = 100000,
                Limit = 50,
                VRPlatform = "Meta Quest"
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.All(results, r => Assert.Contains("Meta Quest", r.VRPlatforms ?? ""));
        }

        [Fact]
        public async Task SearchNearbyAsync_ReturnsEmptyForFarAwayLocation()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var request = new ArcadeSearchRequest
            {
                Latitude = -33.8688, // Sydney, Australia
                Longitude = 151.2093,
                Radius = 10000,
                Limit = 50
            };

            // Act
            var results = await _service.SearchNearbyAsync(request);

            // Assert
            Assert.Empty(results);
        }

        #endregion

        #region FilterArcadesAsync Tests

        [Fact]
        public async Task FilterArcadesAsync_FiltersByCountry()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.FilterArcadesAsync("United States", null, false, false, null);

            // Assert
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task FilterArcadesAsync_FiltersByCity()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.FilterArcadesAsync(null, "New York", false, false, null);

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.Equal("New York", r.City));
        }

        [Fact]
        public async Task FilterArcadesAsync_FiltersByMinRating()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.FilterArcadesAsync(null, null, false, false, 4.0);

            // Assert
            Assert.All(results, r => Assert.True(r.AverageRating >= 4.0m));
        }

        [Fact]
        public async Task FilterArcadesAsync_FiltersByVRPlatform()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.FilterArcadesAsync(null, null, false, false, null, "Meta Quest");

            // Assert
            Assert.All(results, r => Assert.Contains("Meta Quest", r.VRPlatforms ?? ""));
        }

        [Fact]
        public async Task FilterArcadesAsync_ReturnsOrderedByRating()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.FilterArcadesAsync(null, null, false, false, null);

            // Assert
            if (results.Count > 1)
            {
                for (int i = 1; i < results.Count; i++)
                {
                    Assert.True(
                        (results[i].AverageRating ?? 0) <= (results[i - 1].AverageRating ?? 0),
                        "Results should be ordered by rating descending");
                }
            }
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ReturnsArcadeWithIncludes()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var arcade = _context.Arcades.First();

            // Act
            var result = await _service.GetByIdAsync(arcade.ArcadeId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(arcade.Name, result.Name);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullForInvalidId()
        {
            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNullForInactiveArcade()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Inactive",
                Address = "123 St",
                City = "NY",
                Slug = "inactive",
                IsActive = false
            };
            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.GetByIdAsync(arcade.ArcadeId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetBySlugAsync Tests

        [Fact]
        public async Task GetBySlugAsync_ReturnsCorrectArcade()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var result = await _service.GetBySlugAsync("vr-arcade-1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("VR Arcade 1", result.Name);
        }

        [Fact]
        public async Task GetBySlugAsync_ReturnsNullForNonExistentSlug()
        {
            // Act
            var result = await _service.GetBySlugAsync("non-existent-slug");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_SetsTimestampsAndSaves()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "New VR Arcade",
                Address = "789 New St",
                City = "Chicago",
                Slug = "new-vr-arcade"
            };

            // Act
            var result = await _service.CreateAsync(arcade);

            // Assert
            Assert.True(result.ArcadeId > 0);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
            Assert.True(result.UpdatedAt <= DateTime.UtcNow);
            Assert.Equal(1, _context.Arcades.Count());
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_UpdatesTimestampAndSaves()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var arcade = _context.Arcades.First();
            var originalUpdatedAt = arcade.UpdatedAt;

            // Wait a tiny bit to ensure different timestamp
            await Task.Delay(10);

            arcade.Name = "Updated Name";

            // Act
            await _service.UpdateAsync(arcade);

            // Assert
            var updated = await _context.Arcades.FindAsync(arcade.ArcadeId);
            Assert.Equal("Updated Name", updated!.Name);
            Assert.True(updated.UpdatedAt >= originalUpdatedAt);
        }

        #endregion

        #region AddArcadeAsync Tests

        [Fact]
        public async Task AddArcadeAsync_AddsNewArcade()
        {
            // Arrange
            var arcade = new Arcade
            {
                Name = "Brand New VR",
                Address = "101 Fresh St",
                City = "Seattle",
                Slug = "brand-new-vr"
            };

            // Act
            var result = await _service.AddArcadeAsync(arcade);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ArcadeId > 0);
        }

        [Fact]
        public async Task AddArcadeAsync_ReturnExistingIfDuplicateSlug()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var duplicate = new Arcade
            {
                Name = "Duplicate",
                Address = "Dup St",
                City = "NY",
                Slug = "vr-arcade-1" // Already exists
            };

            // Act
            var result = await _service.AddArcadeAsync(duplicate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("VR Arcade 1", result.Name); // Returns existing
        }

        #endregion

        #region AddReviewsAsync Tests

        [Fact]
        public async Task AddReviewsAsync_AddsNewReviews()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var arcade = _context.Arcades.First();
            var reviews = new List<Review>
            {
                new Review { ArcadeId = arcade.ArcadeId, Rating = 5, Content = "Great VR experience!" },
                new Review { ArcadeId = arcade.ArcadeId, Rating = 4, Content = "Very immersive" }
            };

            // Act
            await _service.AddReviewsAsync(arcade.ArcadeId, reviews);

            // Assert
            var savedReviews = _context.Reviews.Where(r => r.ArcadeId == arcade.ArcadeId).ToList();
            Assert.Equal(2, savedReviews.Count);
        }

        [Fact]
        public async Task AddReviewsAsync_SkipsDuplicateContent()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var arcade = _context.Arcades.First();

            // Add first review
            _context.Reviews.Add(new Review { ArcadeId = arcade.ArcadeId, Rating = 5, Content = "Existing review" });
            await _context.SaveChangesAsync();

            // Try adding duplicate
            var reviews = new List<Review>
            {
                new Review { ArcadeId = arcade.ArcadeId, Rating = 5, Content = "Existing review" },
                new Review { ArcadeId = arcade.ArcadeId, Rating = 3, Content = "New review" }
            };

            // Act
            await _service.AddReviewsAsync(arcade.ArcadeId, reviews);

            // Assert
            var savedReviews = _context.Reviews.Where(r => r.ArcadeId == arcade.ArcadeId).ToList();
            Assert.Equal(2, savedReviews.Count); // 1 existing + 1 new (duplicate skipped)
        }

        [Fact]
        public async Task AddReviewsAsync_HandlesEmptyList()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);
            var arcade = _context.Arcades.First();

            // Act
            await _service.AddReviewsAsync(arcade.ArcadeId, new List<Review>());

            // Assert - no exception thrown
            Assert.Empty(_context.Reviews.Where(r => r.ArcadeId == arcade.ArcadeId));
        }

        #endregion

        #region ArcadeExistsAsync Tests

        [Fact]
        public async Task ArcadeExistsAsync_ReturnsTrueForExisting()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var result = await _service.ArcadeExistsAsync("vr-arcade-1");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ArcadeExistsAsync_ReturnsFalseForNonExistent()
        {
            // Act
            var result = await _service.ArcadeExistsAsync("does-not-exist");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region SearchByTextAsync Tests

        [Fact]
        public async Task SearchByTextAsync_FindsByName()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.SearchByTextAsync("VR Arcade 1");

            // Assert
            Assert.NotEmpty(results);
            Assert.Contains(results, a => a.Name == "VR Arcade 1");
        }

        [Fact]
        public async Task SearchByTextAsync_FindsByCity()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.SearchByTextAsync("New York");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, a => Assert.Equal("New York", a.City));
        }

        [Fact]
        public async Task SearchByTextAsync_RespectsLimit()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context, 10);

            // Act
            var results = await _service.SearchByTextAsync("VR", 3);

            // Assert
            Assert.True(results.Count <= 3);
        }

        [Fact]
        public async Task SearchByTextAsync_ReturnsEmptyForBlankQuery()
        {
            // Act
            var results = await _service.SearchByTextAsync("");

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task SearchByTextAsync_ReturnsEmptyForWhitespaceQuery()
        {
            // Act
            var results = await _service.SearchByTextAsync("   ");

            // Assert
            Assert.Empty(results);
        }

        #endregion

        #region GetByCityAsync Tests

        [Fact]
        public async Task GetByCityAsync_ReturnsCorrectArcades()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.GetByCityAsync("New York");

            // Assert
            Assert.NotEmpty(results);
            Assert.All(results, a => Assert.Equal("New York", a.City));
        }

        [Fact]
        public async Task GetByCityAsync_ReturnsOrderedByName()
        {
            // Arrange
            TestDataSeeder.SeedArcades(_context);

            // Act
            var results = await _service.GetByCityAsync("New York");

            // Assert
            if (results.Count > 1)
            {
                for (int i = 1; i < results.Count; i++)
                {
                    Assert.True(string.Compare(results[i].Name, results[i - 1].Name, StringComparison.Ordinal) >= 0);
                }
            }
        }

        #endregion
    }
}
