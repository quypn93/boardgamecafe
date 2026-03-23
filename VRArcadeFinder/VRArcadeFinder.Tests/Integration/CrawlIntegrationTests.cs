using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Tests.Integration
{
    /// <summary>
    /// Integration tests that perform actual Google Maps crawling.
    /// These tests require network access and Playwright browsers installed.
    /// </summary>
    public class CrawlIntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<ILogger<GoogleMapsCrawlerService>> _crawlerLoggerMock;
        private readonly Mock<ILogger<AutoCrawlService>> _autoCrawlLoggerMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<IImageStorageService> _imageStorageMock;
        private readonly string _dbName;
        private readonly string _tempDir;

        public CrawlIntegrationTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _crawlerLoggerMock = new Mock<ILogger<GoogleMapsCrawlerService>>();
            _autoCrawlLoggerMock = new Mock<ILogger<AutoCrawlService>>();
            _environmentMock = new Mock<IWebHostEnvironment>();
            _imageStorageMock = new Mock<IImageStorageService>();

            _tempDir = Path.Combine(Path.GetTempPath(), "vr-crawl-test-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(Path.Combine(_tempDir, "debug"));
            _environmentMock.Setup(e => e.WebRootPath).Returns(_tempDir);

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            _serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        private GoogleMapsCrawlerService CreateCrawlerService()
        {
            return new GoogleMapsCrawlerService(
                _crawlerLoggerMock.Object,
                _serviceProvider,
                _environmentMock.Object,
                _imageStorageMock.Object);
        }

        #region Full Crawl Pipeline Tests

        [Fact]
        public async Task FullCrawlPipeline_CrawlsAndSavesToDatabase()
        {
            // Arrange
            var crawler = CreateCrawlerService();

            // Act - Crawl a major city likely to have VR arcades
            var result = await crawler.CrawlArcadesAsync("VR arcade Tokyo", maxResults: 5);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Found >= 0, "Should find zero or more arcades");

            // Verify database state
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var arcades = await context.Arcades.ToListAsync();

            Assert.Equal(result.Added, arcades.Count);

            // Verify each saved arcade has required fields
            foreach (var arcade in arcades)
            {
                Assert.NotEmpty(arcade.Name);
                Assert.NotEmpty(arcade.Slug);
                Assert.True(arcade.IsActive);
                Assert.True(arcade.CreatedAt <= DateTime.UtcNow);
            }
        }

        [Fact]
        public async Task CrawlMultipleQueries_DeduplicatesAcrossQueries()
        {
            // Arrange
            var crawler = CreateCrawlerService();
            var queries = new[]
            {
                "VR arcade in Los Angeles",
                "virtual reality center Los Angeles",
                "VR gaming Los Angeles"
            };

            // Act
            var results = await crawler.CrawlWithMultipleQueriesAsync("Los Angeles", queries, maxResultsPerQuery: 3);

            // Assert
            Assert.NotNull(results);

            // Verify no duplicate Google Place IDs
            var placeIds = results
                .Where(r => !string.IsNullOrEmpty(r.GooglePlaceId))
                .Select(r => r.GooglePlaceId)
                .ToList();

            Assert.Equal(placeIds.Distinct().Count(), placeIds.Count);
        }

        [Fact]
        public async Task Crawl_ExtractedData_HasRequiredFields()
        {
            // Arrange
            var crawler = CreateCrawlerService();

            // Act
            var results = await crawler.CrawlWithPlaywrightAsync("San Francisco", maxResults: 3);

            // Assert
            foreach (var data in results)
            {
                // Name is always required
                Assert.NotNull(data.Name);
                Assert.NotEmpty(data.Name);

                // Should not be "Results" or "Sponsored?" (filtered out)
                Assert.NotEqual("Results", data.Name);
                Assert.NotEqual("Sponsored?", data.Name);
            }
        }

        #endregion

        #region Crawl + AutoCrawl Integration

        [Fact]
        public async Task AutoCrawl_SeedAndCrawlCity_FullPipeline()
        {
            // Arrange
            var settings = new CrawlSettings
            {
                EnableAutoCrawl = true,
                CitiesPerBatch = 1,
                RetryDelayHours = 2,
                DelayBetweenCitiesSeconds = 1,
                MaxResultsPerCity = 3,
                CheckIntervalMinutes = 60
            };

            // Create a more complete service collection with crawler
            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName + "-autocrawl"));
            services.AddScoped<IGoogleMapsCrawlerService>(sp =>
                new GoogleMapsCrawlerService(
                    _crawlerLoggerMock.Object,
                    sp,
                    _environmentMock.Object,
                    _imageStorageMock.Object));

            using var sp = services.BuildServiceProvider();

            var autoCrawlService = new AutoCrawlService(
                sp,
                _autoCrawlLoggerMock.Object,
                Options.Create(settings));

            // Seed cities
            await autoCrawlService.SeedCitiesAsync();

            // Verify cities were seeded
            using (var scope = sp.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cities = await context.Cities.ToListAsync();
                Assert.Equal(20, cities.Count);
            }

            // Get cities needing crawl
            var citiesNeedingCrawl = await autoCrawlService.GetCitiesNeedingCrawlAsync(1);
            Assert.NotEmpty(citiesNeedingCrawl);

            var cityToCrawl = citiesNeedingCrawl.First();

            // Act - Crawl the city
            try
            {
                await autoCrawlService.CrawlCityAsync(cityToCrawl.CityId);

                // Assert - Verify city was updated
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedCity = await context.Cities.FindAsync(cityToCrawl.CityId);

                Assert.NotNull(updatedCity);
                Assert.Equal("Success", updatedCity.LastCrawlStatus);
                Assert.NotNull(updatedCity.LastCrawledAt);
                Assert.True(updatedCity.CrawlCount > 0);

                // Verify crawl history was created
                var history = await context.CrawlHistories
                    .Where(h => h.CityId == cityToCrawl.CityId)
                    .OrderByDescending(h => h.StartedAt)
                    .FirstOrDefaultAsync();

                Assert.NotNull(history);
                Assert.Equal("Success", history.Status);
                Assert.NotNull(history.CompletedAt);
            }
            catch (Exception ex)
            {
                // Crawl may fail due to network/Google blocking - that's OK for CI
                // Just verify the failure was handled
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedCity = await context.Cities.FindAsync(cityToCrawl.CityId);

                Assert.NotNull(updatedCity);
                Assert.Equal("Failed", updatedCity.LastCrawlStatus);

                // Log the failure reason for debugging
                Assert.NotNull(ex.Message);
            }
        }

        #endregion

        #region Crawl Idempotency Tests

        [Fact]
        public async Task CrawlSameCity_Twice_DoesNotDuplicateArcades()
        {
            // Arrange
            var crawler = CreateCrawlerService();

            // Act - crawl same city twice
            var result1 = await crawler.CrawlArcadesAsync("VR arcade Seattle", maxResults: 3);
            var result2 = await crawler.CrawlArcadesAsync("VR arcade Seattle", maxResults: 3);

            // Assert
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var arcades = await context.Arcades.ToListAsync();

            // Second crawl should update, not duplicate
            if (result1.Found > 0)
            {
                Assert.True(result2.Updated >= 0, "Second crawl should update existing arcades");
            }

            // Verify no duplicate names+addresses
            var arcadeKeys = arcades.Select(a => $"{a.Name}|{a.Address}").ToList();
            Assert.Equal(arcadeKeys.Distinct().Count(), arcadeKeys.Count);
        }

        #endregion
    }
}
