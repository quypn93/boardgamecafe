using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models;
using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Services
{
    public interface IAutoCrawlService
    {
        Task SeedCitiesAsync();
        Task<List<City>> GetCitiesNeedingCrawlAsync(int count);
        Task UpdateCityStatusAsync(int cityId, string status, int arcadesFound, int arcadesAdded, int arcadesUpdated, string? errorMessage = null);
        Task ScheduleRetryAsync(int cityId, int delayHours);
        Task CrawlCityAsync(int cityId);
        Task RunCrawlCycleAsync();
        bool IsEnabled { get; }
    }

    public class AutoCrawlService : BackgroundService, IAutoCrawlService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoCrawlService> _logger;
        private readonly CrawlSettings _settings;

        public bool IsEnabled => _settings.EnableAutoCrawl;

        public AutoCrawlService(
            IServiceProvider serviceProvider,
            ILogger<AutoCrawlService> logger,
            IOptions<CrawlSettings> settings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.EnableAutoCrawl)
            {
                _logger.LogInformation("Auto crawl is disabled");
                return;
            }

            _logger.LogInformation("Auto crawl service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessCrawlBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in auto crawl batch processing");
                }

                await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
            }
        }

        private async Task ProcessCrawlBatchAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var crawlerService = scope.ServiceProvider.GetService<IGoogleMapsCrawlerService>();

            if (crawlerService == null)
            {
                _logger.LogWarning("GoogleMapsCrawlerService not available");
                return;
            }

            var cities = await GetCitiesNeedingCrawlAsync(_settings.CitiesPerBatch);

            if (!cities.Any())
            {
                _logger.LogInformation("No cities need crawling at this time");
                return;
            }

            foreach (var city in cities)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Starting crawl for city: {City}, {Country}", city.Name, city.Country);

                    // Create crawl history record
                    var history = new CrawlHistory
                    {
                        CityId = city.CityId,
                        StartedAt = DateTime.UtcNow,
                        Status = "InProgress"
                    };
                    context.CrawlHistories.Add(history);
                    await context.SaveChangesAsync();

                    // Perform crawl
                    var searchQuery = $"VR arcade {city.Name}";
                    if (!string.IsNullOrEmpty(city.Country))
                    {
                        searchQuery += $" {city.Country}";
                    }

                    var results = await crawlerService.CrawlArcadesAsync(searchQuery, city.MaxResults);

                    // Update history
                    history.CompletedAt = DateTime.UtcNow;
                    history.Status = "Success";
                    history.ArcadesFound = results.Found;
                    history.ArcadesAdded = results.Added;
                    history.ArcadesUpdated = results.Updated;

                    // Update city
                    city.LastCrawledAt = DateTime.UtcNow;
                    city.LastCrawlStatus = "Success";
                    city.CrawlCount++;
                    city.NextCrawlAt = null;

                    await context.SaveChangesAsync();

                    _logger.LogInformation("Completed crawl for {City}: Found={Found}, Added={Added}, Updated={Updated}",
                        city.Name, results.Found, results.Added, results.Updated);

                    // Delay between cities
                    await Task.Delay(TimeSpan.FromSeconds(_settings.DelayBetweenCitiesSeconds), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crawling city: {City}", city.Name);

                    await UpdateCityStatusAsync(city.CityId, "Failed", 0, 0, 0, ex.Message);
                    await ScheduleRetryAsync(city.CityId, _settings.RetryDelayHours);
                }
            }
        }

        public async Task SeedCitiesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (await context.Cities.AnyAsync())
            {
                _logger.LogInformation("Cities already seeded");
                return;
            }

            var cities = new List<City>
            {
                // US Cities
                new City { Name = "New York", Country = "United States", Region = "US" },
                new City { Name = "Los Angeles", Country = "United States", Region = "US" },
                new City { Name = "Chicago", Country = "United States", Region = "US" },
                new City { Name = "Houston", Country = "United States", Region = "US" },
                new City { Name = "Phoenix", Country = "United States", Region = "US" },
                new City { Name = "San Francisco", Country = "United States", Region = "US" },
                new City { Name = "Seattle", Country = "United States", Region = "US" },
                new City { Name = "Denver", Country = "United States", Region = "US" },
                new City { Name = "Austin", Country = "United States", Region = "US" },
                new City { Name = "Las Vegas", Country = "United States", Region = "US" },

                // International Cities
                new City { Name = "Tokyo", Country = "Japan", Region = "International" },
                new City { Name = "Seoul", Country = "South Korea", Region = "International" },
                new City { Name = "London", Country = "United Kingdom", Region = "International" },
                new City { Name = "Berlin", Country = "Germany", Region = "International" },
                new City { Name = "Paris", Country = "France", Region = "International" },
                new City { Name = "Sydney", Country = "Australia", Region = "International" },
                new City { Name = "Toronto", Country = "Canada", Region = "International" },
                new City { Name = "Singapore", Country = "Singapore", Region = "International" },
                new City { Name = "Hong Kong", Country = "Hong Kong", Region = "International" },
                new City { Name = "Dubai", Country = "United Arab Emirates", Region = "International" },
            };

            context.Cities.AddRange(cities);
            await context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} cities for auto crawl", cities.Count);
        }

        public async Task<List<City>> GetCitiesNeedingCrawlAsync(int count)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.UtcNow;

            return await context.Cities
                .Where(c => c.IsActive)
                .Where(c => c.LastCrawledAt == null ||
                           c.NextCrawlAt <= now ||
                           c.CrawlCount == 0)
                .OrderBy(c => c.CrawlCount)
                .ThenBy(c => c.LastCrawledAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task UpdateCityStatusAsync(int cityId, string status, int arcadesFound, int arcadesAdded, int arcadesUpdated, string? errorMessage = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var city = await context.Cities.FindAsync(cityId);
            if (city == null) return;

            city.LastCrawledAt = DateTime.UtcNow;
            city.LastCrawlStatus = status;

            if (status == "Success")
            {
                city.CrawlCount++;
                city.NextCrawlAt = null;
            }

            // Update latest crawl history
            var history = await context.CrawlHistories
                .Where(h => h.CityId == cityId)
                .OrderByDescending(h => h.StartedAt)
                .FirstOrDefaultAsync();

            if (history != null)
            {
                history.CompletedAt = DateTime.UtcNow;
                history.Status = status;
                history.ArcadesFound = arcadesFound;
                history.ArcadesAdded = arcadesAdded;
                history.ArcadesUpdated = arcadesUpdated;
                history.ErrorMessage = errorMessage;
            }

            await context.SaveChangesAsync();
        }

        public async Task ScheduleRetryAsync(int cityId, int delayHours)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var city = await context.Cities.FindAsync(cityId);
            if (city == null) return;

            city.NextCrawlAt = DateTime.UtcNow.AddHours(delayHours);
            await context.SaveChangesAsync();

            _logger.LogInformation("Scheduled retry for city {City} at {Time}", city.Name, city.NextCrawlAt);
        }

        public async Task CrawlCityAsync(int cityId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var crawlerService = scope.ServiceProvider.GetService<IGoogleMapsCrawlerService>();

            if (crawlerService == null)
            {
                _logger.LogWarning("GoogleMapsCrawlerService not available");
                return;
            }

            var city = await context.Cities.FindAsync(cityId);
            if (city == null)
            {
                _logger.LogWarning("City {CityId} not found", cityId);
                return;
            }

            try
            {
                _logger.LogInformation("Starting manual crawl for city: {City}, {Country}", city.Name, city.Country);

                // Create crawl history record
                var history = new CrawlHistory
                {
                    CityId = city.CityId,
                    StartedAt = DateTime.UtcNow,
                    Status = "InProgress"
                };
                context.CrawlHistories.Add(history);
                await context.SaveChangesAsync();

                // Perform crawl
                var searchQuery = $"VR arcade {city.Name}";
                if (!string.IsNullOrEmpty(city.Country))
                {
                    searchQuery += $" {city.Country}";
                }

                var results = await crawlerService.CrawlArcadesAsync(searchQuery, city.MaxResults);

                // Update history
                history.CompletedAt = DateTime.UtcNow;
                history.Status = "Success";
                history.ArcadesFound = results.Found;
                history.ArcadesAdded = results.Added;
                history.ArcadesUpdated = results.Updated;

                // Update city
                city.LastCrawledAt = DateTime.UtcNow;
                city.LastCrawlStatus = "Success";
                city.CrawlCount++;
                city.NextCrawlAt = null;

                await context.SaveChangesAsync();

                _logger.LogInformation("Completed manual crawl for {City}: Found={Found}, Added={Added}, Updated={Updated}",
                    city.Name, results.Found, results.Added, results.Updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manual crawl for city: {City}", city.Name);
                await UpdateCityStatusAsync(city.CityId, "Failed", 0, 0, 0, ex.Message);
                throw;
            }
        }

        public async Task RunCrawlCycleAsync()
        {
            _logger.LogInformation("Starting manual crawl cycle for all active cities");
            await ProcessCrawlBatchAsync(CancellationToken.None);
        }
    }
}
