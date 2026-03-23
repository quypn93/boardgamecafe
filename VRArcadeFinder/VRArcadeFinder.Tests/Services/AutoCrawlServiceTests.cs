using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;
using VRArcadeFinder.Tests.Helpers;

namespace VRArcadeFinder.Tests.Services
{
    public class AutoCrawlServiceTests : IDisposable
    {
        private readonly string _dbName;
        private readonly ServiceProvider _serviceProvider;
        private readonly AutoCrawlService _service;
        private readonly Mock<ILogger<AutoCrawlService>> _loggerMock;
        private readonly CrawlSettings _settings;

        public AutoCrawlServiceTests()
        {
            _dbName = Guid.NewGuid().ToString();
            _loggerMock = new Mock<ILogger<AutoCrawlService>>();
            _settings = new CrawlSettings
            {
                EnableAutoCrawl = true,
                CitiesPerBatch = 3,
                RetryDelayHours = 2,
                DelayBetweenCitiesSeconds = 1,
                MaxResultsPerCity = 15,
                CheckIntervalMinutes = 30
            };

            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
            services.AddScoped<IGoogleMapsCrawlerService>(sp =>
                Mock.Of<IGoogleMapsCrawlerService>());

            _serviceProvider = services.BuildServiceProvider();

            _service = new AutoCrawlService(
                _serviceProvider,
                _loggerMock.Object,
                Options.Create(_settings));
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }

        private ApplicationDbContext GetContext()
        {
            using var scope = _serviceProvider.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        #region IsEnabled Tests

        [Fact]
        public void IsEnabled_ReturnsTrue_WhenAutoCrawlEnabled()
        {
            Assert.True(_service.IsEnabled);
        }

        [Fact]
        public void IsEnabled_ReturnsFalse_WhenAutoCrawlDisabled()
        {
            var settings = new CrawlSettings { EnableAutoCrawl = false };
            var service = new AutoCrawlService(
                _serviceProvider,
                _loggerMock.Object,
                Options.Create(settings));

            Assert.False(service.IsEnabled);
        }

        #endregion

        #region SeedCitiesAsync Tests

        [Fact]
        public async Task SeedCitiesAsync_SeedsCities_WhenEmpty()
        {
            // Act
            await _service.SeedCitiesAsync();

            // Assert
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cities = await context.Cities.ToListAsync();

            Assert.Equal(20, cities.Count);
            Assert.Contains(cities, c => c.Name == "New York");
            Assert.Contains(cities, c => c.Name == "Tokyo");
            Assert.Contains(cities, c => c.Region == "US");
            Assert.Contains(cities, c => c.Region == "International");
        }

        [Fact]
        public async Task SeedCitiesAsync_DoesNotDuplicate_WhenAlreadySeeded()
        {
            // Arrange
            await _service.SeedCitiesAsync();

            // Act
            await _service.SeedCitiesAsync(); // Second call

            // Assert
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(20, await context.Cities.CountAsync());
        }

        #endregion

        #region GetCitiesNeedingCrawlAsync Tests

        [Fact]
        public async Task GetCitiesNeedingCrawlAsync_ReturnsCitiesNeverCrawled()
        {
            // Arrange
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                TestDataSeeder.SeedCities(context);
            }

            // Act
            var results = await _service.GetCitiesNeedingCrawlAsync(10);

            // Assert
            Assert.NotEmpty(results);
            // Cities with CrawlCount == 0 should be returned
            Assert.Contains(results, c => c.CrawlCount == 0);
        }

        [Fact]
        public async Task GetCitiesNeedingCrawlAsync_ExcludesDisabledCities()
        {
            // Arrange
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                TestDataSeeder.SeedCities(context);
            }

            // Act
            var results = await _service.GetCitiesNeedingCrawlAsync(10);

            // Assert
            Assert.DoesNotContain(results, c => c.Name == "Disabled City");
        }

        [Fact]
        public async Task GetCitiesNeedingCrawlAsync_RespectsCountLimit()
        {
            // Arrange
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                TestDataSeeder.SeedCities(context);
            }

            // Act
            var results = await _service.GetCitiesNeedingCrawlAsync(2);

            // Assert
            Assert.True(results.Count <= 2);
        }

        [Fact]
        public async Task GetCitiesNeedingCrawlAsync_OrdersByCrawlCountThenLastCrawled()
        {
            // Arrange
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                TestDataSeeder.SeedCities(context);
            }

            // Act
            var results = await _service.GetCitiesNeedingCrawlAsync(10);

            // Assert
            if (results.Count > 1)
            {
                for (int i = 1; i < results.Count; i++)
                {
                    Assert.True(results[i].CrawlCount >= results[i - 1].CrawlCount,
                        "Should be ordered by CrawlCount ascending");
                }
            }
        }

        #endregion

        #region UpdateCityStatusAsync Tests

        [Fact]
        public async Task UpdateCityStatusAsync_UpdatesCitySuccessStatus()
        {
            // Arrange
            int cityId;
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var city = new City { Name = "Test City", Country = "US", CrawlCount = 0 };
                context.Cities.Add(city);
                await context.SaveChangesAsync();
                cityId = city.CityId;
            }

            // Act
            await _service.UpdateCityStatusAsync(cityId, "Success", 10, 5, 3);

            // Assert
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var city = await context.Cities.FindAsync(cityId);
                Assert.NotNull(city);
                Assert.Equal("Success", city.LastCrawlStatus);
                Assert.NotNull(city.LastCrawledAt);
                Assert.Equal(1, city.CrawlCount);
                Assert.Null(city.NextCrawlAt);
            }
        }

        [Fact]
        public async Task UpdateCityStatusAsync_HandlesNonExistentCity()
        {
            // Act - should not throw
            await _service.UpdateCityStatusAsync(999, "Success", 0, 0, 0);
        }

        #endregion

        #region ScheduleRetryAsync Tests

        [Fact]
        public async Task ScheduleRetryAsync_SetsNextCrawlAt()
        {
            // Arrange
            int cityId;
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var city = new City { Name = "Retry City", Country = "US" };
                context.Cities.Add(city);
                await context.SaveChangesAsync();
                cityId = city.CityId;
            }

            // Act
            await _service.ScheduleRetryAsync(cityId, 2);

            // Assert
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var city = await context.Cities.FindAsync(cityId);
                Assert.NotNull(city);
                Assert.NotNull(city.NextCrawlAt);
                // Should be approximately 2 hours from now
                var expectedTime = DateTime.UtcNow.AddHours(2);
                Assert.True(Math.Abs((city.NextCrawlAt.Value - expectedTime).TotalMinutes) < 1);
            }
        }

        [Fact]
        public async Task ScheduleRetryAsync_HandlesNonExistentCity()
        {
            // Act - should not throw
            await _service.ScheduleRetryAsync(999, 2);
        }

        #endregion

        #region CrawlCityAsync Tests

        [Fact]
        public async Task CrawlCityAsync_LogsWarning_WhenCityNotFound()
        {
            // Act
            await _service.CrawlCityAsync(999);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }
}
