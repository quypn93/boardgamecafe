using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models;
using EscapeRoomFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Services
{
    public class AutoCrawlService : BackgroundService, IAutoCrawlService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoCrawlService> _logger;
        private readonly CrawlSettings _settings;
        private CancellationTokenSource? _stopCts;

        public bool IsRunning { get; private set; }
        public string? CurrentCity { get; private set; }

        public AutoCrawlService(
            IServiceScopeFactory scopeFactory,
            ILogger<AutoCrawlService> logger,
            IOptions<CrawlSettings> settings)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _settings = settings.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoCrawlService started. AutoCrawl enabled: {Enabled}", _settings.EnableAutoCrawl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_settings.EnableAutoCrawl)
                    {
                        await RunCrawlBatchAsync(stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AutoCrawlService main loop");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("AutoCrawlService stopped");
        }

        public async Task RunCrawlBatchAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Crawl batch already running, skipping");
                return;
            }

            IsRunning = true;
            _stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var cities = await GetNextCitiesToCrawlAsync(context);

                if (!cities.Any())
                {
                    _logger.LogInformation("No cities to crawl");
                    return;
                }

                _logger.LogInformation("Starting crawl batch for {Count} cities", cities.Count);

                foreach (var city in cities)
                {
                    if (_stopCts.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Crawl batch stopped by user");
                        break;
                    }

                    CurrentCity = $"{city.Name}, {city.Country}";
                    _logger.LogInformation("Crawling city: {City}", CurrentCity);

                    var (venuesFound, venuesAdded, venuesUpdated, error) = await CrawlCityInternalAsync(city, context, _stopCts.Token);

                    if (error != null)
                    {
                        _logger.LogWarning("Crawl failed for {City}: {Error}", CurrentCity, error);
                        break; // Stop batch on failure
                    }

                    _logger.LogInformation("Completed {City}: Found={Found}, Added={Added}, Updated={Updated}",
                        CurrentCity, venuesFound, venuesAdded, venuesUpdated);

                    // Delay between cities
                    if (cities.IndexOf(city) < cities.Count - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_settings.DelayBetweenCitiesSeconds), _stopCts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Crawl batch cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in crawl batch");
            }
            finally
            {
                IsRunning = false;
                CurrentCity = null;
                _stopCts?.Dispose();
                _stopCts = null;
            }
        }

        public async Task<(int venuesFound, int venuesAdded, int venuesUpdated, string? error)> CrawlCityAsync(int cityId, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var city = await context.Cities.FindAsync(cityId);
            if (city == null)
            {
                return (0, 0, 0, "City not found");
            }

            CurrentCity = $"{city.Name}, {city.Country}";
            IsRunning = true;

            try
            {
                return await CrawlCityInternalAsync(city, context, cancellationToken);
            }
            finally
            {
                IsRunning = false;
                CurrentCity = null;
            }
        }

        private async Task<(int venuesFound, int venuesAdded, int venuesUpdated, string? error)> CrawlCityInternalAsync(
            City city, ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var history = new CrawlHistory
            {
                CityId = city.CityId,
                StartedAt = DateTime.UtcNow,
                Status = "InProgress"
            };

            context.CrawlHistories.Add(history);
            await context.SaveChangesAsync(cancellationToken);

            try
            {
                using var crawlerScope = _scopeFactory.CreateScope();
                var crawler = crawlerScope.ServiceProvider.GetRequiredService<IGoogleMapsCrawlerService>();

                var location = $"{city.Name}, {city.Country}";
                var results = await crawler.CrawlEscapeRoomsAsync(location, city.MaxResults);

                int venuesAdded = 0;
                int venuesUpdated = 0;

                foreach (var crawledVenue in results)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Skip if no valid location data
                    if (!crawledVenue.Latitude.HasValue || !crawledVenue.Longitude.HasValue)
                    {
                        _logger.LogWarning("Skipping venue {Name} - no coordinates", crawledVenue.Name);
                        continue;
                    }

                    // Check for existing venue by GooglePlaceId
                    var existingVenue = !string.IsNullOrEmpty(crawledVenue.GooglePlaceId)
                        ? await context.Venues.FirstOrDefaultAsync(v => v.GooglePlaceId == crawledVenue.GooglePlaceId, cancellationToken)
                        : null;

                    if (existingVenue != null)
                    {
                        // Update existing venue
                        UpdateVenueFromCrawled(existingVenue, crawledVenue);
                        existingVenue.UpdatedAt = DateTime.UtcNow;
                        venuesUpdated++;
                    }
                    else
                    {
                        // Create new venue
                        var newVenue = CreateVenueFromCrawled(crawledVenue, city);
                        newVenue.Slug = await GenerateUniqueSlugAsync(context, newVenue.Name);
                        context.Venues.Add(newVenue);
                        venuesAdded++;
                    }
                }

                await context.SaveChangesAsync(cancellationToken);

                // Update city status
                city.CrawlCount++;
                city.LastCrawledAt = DateTime.UtcNow;
                city.LastCrawlStatus = "Success";
                city.NextCrawlAt = null;

                // Update history
                history.Status = "Success";
                history.CompletedAt = DateTime.UtcNow;
                history.VenuesFound = results.Count;
                history.VenuesAdded = venuesAdded;
                history.VenuesUpdated = venuesUpdated;

                await context.SaveChangesAsync(cancellationToken);

                return (results.Count, venuesAdded, venuesUpdated, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling city {City}", city.Name);

                city.LastCrawlStatus = "Failed";
                city.NextCrawlAt = DateTime.UtcNow.AddHours(_settings.RetryDelayHours);

                history.Status = "Failed";
                history.CompletedAt = DateTime.UtcNow;
                history.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                await context.SaveChangesAsync(CancellationToken.None);

                return (0, 0, 0, ex.Message);
            }
        }

        private async Task<List<City>> GetNextCitiesToCrawlAsync(ApplicationDbContext context)
        {
            // First check for failed cities that need retry
            var retryNeeded = await context.Cities
                .Where(c => c.IsActive && c.LastCrawlStatus == "Failed" && c.NextCrawlAt <= DateTime.UtcNow)
                .OrderBy(c => c.NextCrawlAt)
                .Take(_settings.CitiesPerBatch)
                .ToListAsync();

            if (retryNeeded.Any())
            {
                return retryNeeded;
            }

            // Then get cities by priority (never crawled first, then oldest)
            return await context.Cities
                .Where(c => c.IsActive)
                .OrderBy(c => c.CrawlCount)
                .ThenBy(c => c.LastCrawledAt)
                .Take(_settings.CitiesPerBatch)
                .ToListAsync();
        }

        private EscapeRoomVenue CreateVenueFromCrawled(CrawledVenueData crawled, City city)
        {
            return new EscapeRoomVenue
            {
                Name = crawled.Name,
                Address = crawled.Address,
                City = city.Name,
                Country = city.Country,
                State = ExtractState(crawled.Address),
                Latitude = crawled.Latitude ?? 0,
                Longitude = crawled.Longitude ?? 0,
                Phone = crawled.Phone,
                Website = crawled.Website,
                GoogleMapsUrl = crawled.GoogleMapsUrl,
                GooglePlaceId = crawled.GooglePlaceId,
                LocalImagePath = crawled.LocalImagePath,
                Description = crawled.Description,
                PriceRange = crawled.PriceLevel,
                AverageRating = crawled.Rating.HasValue ? (decimal)crawled.Rating.Value : null,
                TotalReviews = crawled.ReviewCount ?? 0,
                OpeningHours = crawled.OpeningHours,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private void UpdateVenueFromCrawled(EscapeRoomVenue venue, CrawledVenueData crawled)
        {
            venue.Address = crawled.Address;
            venue.Phone = crawled.Phone ?? venue.Phone;
            venue.Website = crawled.Website ?? venue.Website;
            venue.GoogleMapsUrl = crawled.GoogleMapsUrl ?? venue.GoogleMapsUrl;

            if (crawled.Latitude.HasValue) venue.Latitude = crawled.Latitude.Value;
            if (crawled.Longitude.HasValue) venue.Longitude = crawled.Longitude.Value;
            if (crawled.Rating.HasValue) venue.AverageRating = (decimal)crawled.Rating.Value;
            if (crawled.ReviewCount.HasValue) venue.TotalReviews = crawled.ReviewCount.Value;
            if (!string.IsNullOrEmpty(crawled.OpeningHours)) venue.OpeningHours = crawled.OpeningHours;
            if (!string.IsNullOrEmpty(crawled.LocalImagePath)) venue.LocalImagePath = crawled.LocalImagePath;
        }

        private string? ExtractState(string address)
        {
            // Try to extract state from US address format "City, State ZIP"
            var match = Regex.Match(address, @",\s*([A-Z]{2})\s+\d{5}");
            return match.Success ? match.Groups[1].Value : null;
        }

        private async Task<string> GenerateUniqueSlugAsync(ApplicationDbContext context, string name)
        {
            var baseSlug = GenerateSlug(name);
            var slug = baseSlug;
            var counter = 1;

            while (await context.Venues.AnyAsync(v => v.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Guid.NewGuid().ToString("N")[..8];

            var slug = name.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (string.IsNullOrEmpty(slug))
                return Guid.NewGuid().ToString("N")[..8];

            return slug.Length > 100 ? slug[..100] : slug;
        }

        public void Stop()
        {
            _stopCts?.Cancel();
        }

        public async Task SeedCitiesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (await context.Cities.AnyAsync())
            {
                _logger.LogInformation("Cities already seeded, skipping");
                return;
            }

            var cities = GetDefaultCities();
            context.Cities.AddRange(cities);
            await context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} cities", cities.Count);
        }

        private List<City> GetDefaultCities()
        {
            var cities = new List<City>();

            // US Cities
            var usCities = new[]
            {
                "New York", "Los Angeles", "Chicago", "Houston", "Phoenix",
                "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose",
                "Austin", "Jacksonville", "Fort Worth", "Columbus", "Charlotte",
                "San Francisco", "Indianapolis", "Seattle", "Denver", "Washington",
                "Boston", "El Paso", "Nashville", "Detroit", "Oklahoma City",
                "Portland", "Las Vegas", "Memphis", "Louisville", "Baltimore",
                "Milwaukee", "Albuquerque", "Tucson", "Fresno", "Sacramento",
                "Kansas City", "Mesa", "Atlanta", "Omaha", "Colorado Springs",
                "Raleigh", "Miami", "Long Beach", "Virginia Beach", "Oakland",
                "Minneapolis", "Tampa", "Tulsa", "Arlington", "New Orleans",
                "Wichita", "Cleveland", "Bakersfield", "Aurora", "Anaheim",
                "Honolulu", "Santa Ana", "Riverside", "Corpus Christi", "Lexington",
                "Henderson", "Stockton", "Saint Paul", "Cincinnati", "St. Louis",
                "Pittsburgh", "Greensboro", "Lincoln", "Anchorage", "Plano",
                "Orlando", "Irvine", "Newark", "Durham", "Chula Vista",
                "Toledo", "Fort Wayne", "St. Petersburg", "Laredo", "Jersey City",
                "Chandler", "Madison", "Lubbock", "Scottsdale", "Reno",
                "Buffalo", "Gilbert", "Glendale", "North Las Vegas", "Winston-Salem",
                "Chesapeake", "Norfolk", "Fremont", "Garland", "Irving",
                "Hialeah", "Richmond", "Boise", "Spokane", "Baton Rouge"
            };

            foreach (var city in usCities)
            {
                cities.Add(new City { Name = city, Country = "United States", Region = "US" });
            }

            // International Cities
            var internationalCities = new Dictionary<string, string[]>
            {
                ["United Kingdom"] = new[] { "London", "Manchester", "Birmingham", "Leeds", "Glasgow", "Liverpool", "Bristol", "Edinburgh", "Sheffield", "Newcastle" },
                ["Canada"] = new[] { "Toronto", "Vancouver", "Montreal", "Calgary", "Edmonton", "Ottawa", "Winnipeg", "Quebec City", "Halifax", "Victoria" },
                ["Australia"] = new[] { "Sydney", "Melbourne", "Brisbane", "Perth", "Adelaide", "Gold Coast", "Canberra", "Newcastle", "Wollongong", "Hobart" },
                ["Germany"] = new[] { "Berlin", "Munich", "Hamburg", "Frankfurt", "Cologne", "Stuttgart", "Dusseldorf", "Leipzig", "Dortmund", "Essen" },
                ["France"] = new[] { "Paris", "Marseille", "Lyon", "Toulouse", "Nice", "Nantes", "Strasbourg", "Montpellier", "Bordeaux", "Lille" },
                ["Japan"] = new[] { "Tokyo", "Osaka", "Yokohama", "Nagoya", "Sapporo", "Kobe", "Kyoto", "Fukuoka", "Kawasaki", "Hiroshima" },
                ["South Korea"] = new[] { "Seoul", "Busan", "Incheon", "Daegu", "Daejeon", "Gwangju", "Suwon", "Ulsan" },
                ["Netherlands"] = new[] { "Amsterdam", "Rotterdam", "The Hague", "Utrecht", "Eindhoven" },
                ["Spain"] = new[] { "Madrid", "Barcelona", "Valencia", "Seville", "Bilbao", "Malaga" },
                ["Italy"] = new[] { "Rome", "Milan", "Naples", "Turin", "Florence", "Venice" },
                ["Poland"] = new[] { "Warsaw", "Krakow", "Lodz", "Wroclaw", "Poznan", "Gdansk" },
                ["Czech Republic"] = new[] { "Prague", "Brno", "Ostrava" },
                ["Hungary"] = new[] { "Budapest", "Debrecen", "Szeged" },
                ["Austria"] = new[] { "Vienna", "Graz", "Salzburg", "Innsbruck" },
                ["Switzerland"] = new[] { "Zurich", "Geneva", "Basel", "Bern" },
                ["Belgium"] = new[] { "Brussels", "Antwerp", "Ghent", "Bruges" },
                ["Sweden"] = new[] { "Stockholm", "Gothenburg", "Malmo" },
                ["Norway"] = new[] { "Oslo", "Bergen", "Trondheim" },
                ["Denmark"] = new[] { "Copenhagen", "Aarhus", "Odense" },
                ["Finland"] = new[] { "Helsinki", "Espoo", "Tampere" },
                ["Portugal"] = new[] { "Lisbon", "Porto", "Braga" },
                ["Greece"] = new[] { "Athens", "Thessaloniki" },
                ["Ireland"] = new[] { "Dublin", "Cork", "Galway" },
                ["Singapore"] = new[] { "Singapore" },
                ["Hong Kong"] = new[] { "Hong Kong" },
                ["Taiwan"] = new[] { "Taipei", "Kaohsiung", "Taichung" },
                ["Thailand"] = new[] { "Bangkok", "Chiang Mai", "Phuket" },
                ["Vietnam"] = new[] { "Ho Chi Minh City", "Hanoi", "Da Nang" },
                ["Malaysia"] = new[] { "Kuala Lumpur", "Penang", "Johor Bahru" },
                ["Philippines"] = new[] { "Manila", "Cebu", "Davao" },
                ["Indonesia"] = new[] { "Jakarta", "Bali", "Surabaya" },
                ["India"] = new[] { "Mumbai", "Delhi", "Bangalore", "Chennai", "Kolkata", "Hyderabad" },
                ["United Arab Emirates"] = new[] { "Dubai", "Abu Dhabi" },
                ["Israel"] = new[] { "Tel Aviv", "Jerusalem", "Haifa" },
                ["South Africa"] = new[] { "Johannesburg", "Cape Town", "Durban" },
                ["Brazil"] = new[] { "Sao Paulo", "Rio de Janeiro", "Brasilia", "Salvador" },
                ["Mexico"] = new[] { "Mexico City", "Guadalajara", "Monterrey", "Cancun" },
                ["Argentina"] = new[] { "Buenos Aires", "Cordoba", "Rosario" },
                ["Chile"] = new[] { "Santiago", "Valparaiso" },
                ["Colombia"] = new[] { "Bogota", "Medellin", "Cali" },
                ["New Zealand"] = new[] { "Auckland", "Wellington", "Christchurch" },
                ["Russia"] = new[] { "Moscow", "Saint Petersburg", "Novosibirsk" },
                ["China"] = new[] { "Shanghai", "Beijing", "Shenzhen", "Guangzhou", "Chengdu", "Hangzhou" }
            };

            foreach (var (country, cityNames) in internationalCities)
            {
                foreach (var cityName in cityNames)
                {
                    cities.Add(new City { Name = cityName, Country = country, Region = "International" });
                }
            }

            return cities;
        }
    }
}
