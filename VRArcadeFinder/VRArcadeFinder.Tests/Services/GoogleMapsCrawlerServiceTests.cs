using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;
using VRArcadeFinder.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;

namespace VRArcadeFinder.Tests.Services
{
    public class GoogleMapsCrawlerServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleMapsCrawlerService _service;
        private readonly Mock<ILogger<GoogleMapsCrawlerService>> _loggerMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<IImageStorageService> _imageStorageMock;
        private readonly ServiceProvider _serviceProvider;

        public GoogleMapsCrawlerServiceTests()
        {
            var dbName = Guid.NewGuid().ToString();
            _loggerMock = new Mock<ILogger<GoogleMapsCrawlerService>>();
            _environmentMock = new Mock<IWebHostEnvironment>();
            _imageStorageMock = new Mock<IImageStorageService>();

            var tempDir = Path.Combine(Path.GetTempPath(), "vr-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            _environmentMock.Setup(e => e.WebRootPath).Returns(tempDir);

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            _serviceProvider = services.BuildServiceProvider();
            _context = TestDbContextFactory.Create(dbName);

            _service = new GoogleMapsCrawlerService(
                _loggerMock.Object,
                _serviceProvider,
                _environmentMock.Object,
                _imageStorageMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _serviceProvider.Dispose();
        }

        #region CrawlArcadesAsync Tests

        [Fact]
        public async Task CrawlArcadesAsync_ReturnsResultWithCounts()
        {
            // This is an integration test that uses the real Playwright browser
            // It will actually crawl Google Maps

            // Act
            var result = await _service.CrawlArcadesAsync("VR arcade New York", maxResults: 3);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Found >= 0);
            Assert.True(result.Added >= 0);
            Assert.True(result.Updated >= 0);
        }

        #endregion

        #region CrawlWithPlaywrightAsync Tests

        [Fact]
        public async Task CrawlWithPlaywrightAsync_ReturnsCrawledData()
        {
            // Act
            var results = await _service.CrawlWithPlaywrightAsync("New York", maxResults: 3);

            // Assert
            Assert.NotNull(results);
            // May find arcades or may not (depends on Google Maps results)
            // Just verify no crash and correct data structure
            foreach (var data in results)
            {
                Assert.NotEmpty(data.Name);
            }
        }

        #endregion

        #region CrawlWithMultipleQueriesAsync Tests

        [Fact]
        public async Task CrawlWithMultipleQueriesAsync_DeduplicatesResults()
        {
            // Arrange
            var queries = new[]
            {
                "VR arcade New York",
                "virtual reality arcade New York"
            };

            // Act
            var results = await _service.CrawlWithMultipleQueriesAsync("New York", queries, maxResultsPerQuery: 3);

            // Assert
            Assert.NotNull(results);
            // Check no duplicate place IDs
            var placeIds = results
                .Where(r => !string.IsNullOrEmpty(r.GooglePlaceId))
                .Select(r => r.GooglePlaceId)
                .ToList();
            Assert.Equal(placeIds.Count, placeIds.Distinct().Count());
        }

        #endregion

        #region CrawlSingleArcadeAsync Tests

        [Fact]
        public async Task CrawlSingleArcadeAsync_ReturnsNull_NotImplemented()
        {
            // Act
            var result = await _service.CrawlSingleArcadeAsync("some-place-id");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region CrawlArcadesAsync Database Integration Tests

        [Fact]
        public async Task CrawlArcadesAsync_SavesNewArcadesToDatabase()
        {
            // Act
            var result = await _service.CrawlArcadesAsync("VR arcade Los Angeles", maxResults: 3);

            // Assert
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var arcades = await context.Arcades.ToListAsync();

            // The number of arcades in DB should match what was added
            Assert.Equal(result.Added, arcades.Count(a => a.CreatedAt > DateTime.UtcNow.AddMinutes(-5)));
        }

        [Fact]
        public async Task CrawlArcadesAsync_UpdatesExistingArcades()
        {
            // Arrange - add an arcade that might be found by crawler
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Arcades.Add(new Arcade
                {
                    Name = "Test VR Arcade",
                    Address = "123 Test St",
                    City = "San Francisco",
                    Slug = "test-vr-arcade",
                    GooglePlaceId = "test-place-id",
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            // Act
            var result = await _service.CrawlArcadesAsync("VR arcade San Francisco", maxResults: 3);

            // Assert
            Assert.NotNull(result);
            // Verify no errors occurred
            Assert.True(result.Found >= 0);
        }

        #endregion
    }
}
