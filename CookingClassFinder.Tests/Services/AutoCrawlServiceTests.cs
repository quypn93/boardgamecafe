using CookingClassFinder.Data;
using CookingClassFinder.Models;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using CookingClassFinder.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CookingClassFinder.Tests.Services;

public class AutoCrawlServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ServiceProvider _serviceProvider;
    private readonly AutoCrawlService _service;
    private readonly CrawlSettings _settings;

    public AutoCrawlServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _settings = new CrawlSettings
        {
            EnableAutoCrawl = false, // Disabled for unit tests
            CitiesPerBatch = 3,
            RetryDelayHours = 2,
            DelayBetweenCitiesSeconds = 0, // No delay for tests
            MaxResultsPerCity = 15,
            CheckIntervalMinutes = 30
        };

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();

        // Seed data
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            TestDbContextFactory.SeedTestDataAsync(context).GetAwaiter().GetResult();
        }

        var logger = new Mock<ILogger<AutoCrawlService>>();
        var options = Options.Create(_settings);

        _service = new AutoCrawlService(_serviceProvider, logger.Object, options);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    #region CrawlCityAsync

    [Fact]
    public async Task CrawlCityAsync_ValidCity_ReturnsSuccess()
    {
        var city = new City { CityId = 1, Name = "New York", Country = "United States" };

        var result = await _service.CrawlCityAsync(city);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CrawlCityAsync_ValidCity_CreatesCrawlHistory()
    {
        var city = new City { CityId = 1, Name = "New York", Country = "United States" };

        await _service.CrawlCityAsync(city);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var history = await context.CrawlHistories
            .Where(h => h.CityId == 1)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.Equal("Success", history.Status);
        Assert.NotNull(history.CompletedAt);
    }

    [Fact]
    public async Task CrawlCityAsync_ValidCity_UpdatesCityMetadata()
    {
        var city = new City { CityId = 1, Name = "New York", Country = "United States" };

        await _service.CrawlCityAsync(city);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCity = await context.Cities.FindAsync(1);

        Assert.NotNull(updatedCity);
        Assert.NotNull(updatedCity.LastCrawledAt);
        Assert.Equal("Success", updatedCity.LastCrawlStatus);
        Assert.Equal(1, updatedCity.CrawlCount); // Was 0, incremented to 1
        Assert.NotNull(updatedCity.NextCrawlAt);
    }

    [Fact]
    public async Task CrawlCityAsync_NonExistentCity_ReturnsFailure()
    {
        var city = new City { CityId = 999, Name = "Fake City" };

        var result = await _service.CrawlCityAsync(city);

        Assert.False(result.Success);
        Assert.Equal("City not found", result.ErrorMessage);
    }

    [Fact]
    public async Task CrawlCityAsync_SetsNextCrawlAt_7DaysLater()
    {
        var city = new City { CityId = 1, Name = "New York" };

        var before = DateTime.UtcNow;
        await _service.CrawlCityAsync(city);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updatedCity = await context.Cities.FindAsync(1);

        Assert.NotNull(updatedCity?.NextCrawlAt);
        // NextCrawlAt should be ~7 days from now
        Assert.True(updatedCity.NextCrawlAt > before.AddDays(6));
        Assert.True(updatedCity.NextCrawlAt < before.AddDays(8));
    }

    #endregion

    #region GetNextCitiesToCrawlAsync

    [Fact]
    public async Task GetNextCitiesToCrawlAsync_ReturnsActiveCitiesDueForCrawl()
    {
        var result = await _service.GetNextCitiesToCrawlAsync(10);

        // Active cities with NextCrawlAt null or past: New York (null), Chicago (null), SF (past), Tokyo (past)
        Assert.True(result.Count >= 3);
        Assert.DoesNotContain(result, c => c.Name == "Inactive City");
    }

    [Fact]
    public async Task GetNextCitiesToCrawlAsync_OrderedByCrawlCount()
    {
        var result = await _service.GetNextCitiesToCrawlAsync(10);

        // Should be ordered by CrawlCount ascending
        for (int i = 1; i < result.Count; i++)
        {
            Assert.True(result[i].CrawlCount >= result[i - 1].CrawlCount);
        }
    }

    [Fact]
    public async Task GetNextCitiesToCrawlAsync_RespectsCount()
    {
        var result = await _service.GetNextCitiesToCrawlAsync(2);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetNextCitiesToCrawlAsync_ExcludesInactiveCities()
    {
        var result = await _service.GetNextCitiesToCrawlAsync(10);
        Assert.DoesNotContain(result, c => !c.IsActive);
    }

    [Fact]
    public async Task GetNextCitiesToCrawlAsync_ExcludesFutureCrawlSchedule()
    {
        // Set a city with NextCrawlAt far in the future
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var city = await context.Cities.FindAsync(1);
            Assert.NotNull(city);
            city.NextCrawlAt = DateTime.UtcNow.AddDays(30);
            await context.SaveChangesAsync();
        }

        var result = await _service.GetNextCitiesToCrawlAsync(10);
        Assert.DoesNotContain(result, c => c.CityId == 1);
    }

    #endregion

    #region SeedCitiesAsync

    [Fact]
    public async Task SeedCitiesAsync_WhenCitiesExist_DoesNotDuplicate()
    {
        // Cities already exist from test setup
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var countBefore = await context.Cities.CountAsync();

        await _service.SeedCitiesAsync();

        var countAfter = await context.Cities.CountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task SeedCitiesAsync_EmptyDb_SeedsCities()
    {
        // Create a fresh DB
        var freshDbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(freshDbName));
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var logger = new Mock<ILogger<AutoCrawlService>>();
        var options = Options.Create(_settings);
        var freshService = new AutoCrawlService(sp, logger.Object, options);

        await freshService.SeedCitiesAsync();

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.Cities.CountAsync();

        Assert.True(count > 100); // Should seed 170+ cities
    }

    [Fact]
    public async Task SeedCitiesAsync_IncludesUSAndInternationalCities()
    {
        var freshDbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(freshDbName));
        services.AddLogging();

        using var sp = services.BuildServiceProvider();
        var logger = new Mock<ILogger<AutoCrawlService>>();
        var options = Options.Create(_settings);
        var freshService = new AutoCrawlService(sp, logger.Object, options);

        await freshService.SeedCitiesAsync();

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var usCount = await context.Cities.CountAsync(c => c.Region == "US");
        var intlCount = await context.Cities.CountAsync(c => c.Region == "International");

        Assert.True(usCount > 50);
        Assert.True(intlCount > 50);
    }

    #endregion

    #region Stop

    [Fact]
    public void Stop_SetsIsRunningFalse()
    {
        _service.Stop();
        Assert.False(_service.IsRunning);
    }

    #endregion

    #region IsRunning

    [Fact]
    public void IsRunning_InitiallyFalse()
    {
        Assert.False(_service.IsRunning);
    }

    #endregion
}
