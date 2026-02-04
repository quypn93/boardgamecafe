using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BoardGameCafeFinder.Services
{
    public interface IAutoCrawlService
    {
        Task<CrawlResult> CrawlCityAsync(City city, CancellationToken cancellationToken = default);
        Task<List<City>> GetNextCitiesToCrawlAsync(int count);
        Task SeedCitiesAsync();
        bool IsRunning { get; }
        void Stop();
    }

    public class CrawlResult
    {
        public bool Success { get; set; }
        public int CafesFound { get; set; }
        public int CafesAdded { get; set; }
        public int CafesUpdated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AutoCrawlService : BackgroundService, IAutoCrawlService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoCrawlService> _logger;
        private readonly CrawlSettings _settings;
        private bool _isRunning = false;
        private CancellationTokenSource? _stopTokenSource;

        public bool IsRunning => _isRunning;

        public AutoCrawlService(
            IServiceProvider serviceProvider,
            ILogger<AutoCrawlService> logger,
            IOptions<CrawlSettings> settings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = settings.Value;
        }

        public void Stop()
        {
            _stopTokenSource?.Cancel();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoCrawlService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_settings.EnableAutoCrawl)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                try
                {
                    await RunCrawlBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in auto crawl batch");
                }

                // Wait before next check
                await Task.Delay(TimeSpan.FromMinutes(_settings.CheckIntervalMinutes), stoppingToken);
            }

            _logger.LogInformation("AutoCrawlService stopped");
        }

        private async Task RunCrawlBatchAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Check if any city needs retry (NextCrawlAt has passed)
            var now = DateTime.UtcNow;
            var citiesNeedingRetry = await context.Cities
                .Where(c => c.IsActive && c.NextCrawlAt != null && c.NextCrawlAt <= now)
                .OrderBy(c => c.NextCrawlAt)
                .Take(_settings.CitiesPerBatch)
                .ToListAsync(stoppingToken);

            if (citiesNeedingRetry.Any())
            {
                _logger.LogInformation("Found {Count} cities needing retry", citiesNeedingRetry.Count);
                await CrawlCitiesAsync(citiesNeedingRetry, context, stoppingToken);
                return;
            }

            // Otherwise get next batch of cities to crawl
            var citiesToCrawl = await GetNextCitiesToCrawlInternalAsync(context, _settings.CitiesPerBatch, stoppingToken);

            if (!citiesToCrawl.Any())
            {
                _logger.LogInformation("No cities to crawl at this time");
                return;
            }

            await CrawlCitiesAsync(citiesToCrawl, context, stoppingToken);
        }

        private async Task CrawlCitiesAsync(List<City> cities, ApplicationDbContext context, CancellationToken stoppingToken)
        {
            _isRunning = true;
            _stopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            try
            {
                foreach (var city in cities)
                {
                    if (_stopTokenSource.Token.IsCancellationRequested)
                        break;

                    _logger.LogInformation("Starting crawl for city: {City}", city.Name);

                    var result = await CrawlCityInternalAsync(city, context, _stopTokenSource.Token);

                    if (!result.Success)
                    {
                        // Failed - stop crawling and set retry time
                        city.LastCrawlStatus = "Failed";
                        city.NextCrawlAt = DateTime.UtcNow.AddHours(_settings.RetryDelayHours);
                        await context.SaveChangesAsync(stoppingToken);

                        _logger.LogWarning("Crawl failed for {City}. Will retry after {RetryTime}",
                            city.Name, city.NextCrawlAt);
                        break;
                    }

                    // Success - update city stats
                    city.CrawlCount++;
                    city.LastCrawledAt = DateTime.UtcNow;
                    city.LastCrawlStatus = "Success";
                    city.NextCrawlAt = null;
                    await context.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Crawl completed for {City}. Found: {Found}, Added: {Added}, Updated: {Updated}",
                        city.Name, result.CafesFound, result.CafesAdded, result.CafesUpdated);

                    // Delay between cities
                    if (cities.IndexOf(city) < cities.Count - 1)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_settings.DelayBetweenCitiesSeconds), _stopTokenSource.Token);
                    }
                }
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task<CrawlResult> CrawlCityInternalAsync(City city, ApplicationDbContext context, CancellationToken cancellationToken)
        {
            // Create crawl history record
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
                using var scope = _serviceProvider.CreateScope();
                var crawlerService = scope.ServiceProvider.GetRequiredService<IGoogleMapsCrawlerService>();

                // Format location string
                var location = string.IsNullOrEmpty(city.Country)
                    ? city.Name
                    : $"{city.Name}, {city.Country}";

                var maxResults = city.MaxResults > 0 ? city.MaxResults : _settings.MaxResultsPerCity;
                var crawledCafes = await crawlerService.CrawlBoardGameCafesAsync(location, maxResults);

                int added = 0;
                int updated = 0;
                var usedSlugs = new HashSet<string>();

                foreach (var crawledCafe in crawledCafes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var existingCafe = await context.Cafes
                            .FirstOrDefaultAsync(c => c.GooglePlaceId == crawledCafe.GooglePlaceId && crawledCafe.GooglePlaceId != null, cancellationToken);

                        if (existingCafe != null)
                        {
                            // Update existing cafe
                            existingCafe.Name = crawledCafe.Name;
                            existingCafe.Address = crawledCafe.Address;
                            existingCafe.City = crawledCafe.City ?? existingCafe.City;
                            existingCafe.State = crawledCafe.State ?? existingCafe.State;
                            existingCafe.Country = crawledCafe.Country ?? existingCafe.Country;
                            existingCafe.Phone = crawledCafe.Phone;
                            existingCafe.Website = crawledCafe.Website;
                            existingCafe.Latitude = crawledCafe.Latitude ?? existingCafe.Latitude;
                            existingCafe.Longitude = crawledCafe.Longitude ?? existingCafe.Longitude;
                            existingCafe.AverageRating = crawledCafe.Rating.HasValue ? (decimal)crawledCafe.Rating.Value : existingCafe.AverageRating;
                            if (crawledCafe.ReviewCount.HasValue)
                                existingCafe.TotalReviews = crawledCafe.ReviewCount.Value;
                            existingCafe.PriceRange = crawledCafe.PriceLevel;
                            existingCafe.GoogleMapsUrl = crawledCafe.GoogleMapsUrl;
                            existingCafe.LocalImagePath = crawledCafe.LocalImagePath;
                            existingCafe.BggUsername = crawledCafe.BggUsername ?? existingCafe.BggUsername;
                            existingCafe.UpdatedAt = DateTime.UtcNow;

                            if (crawledCafe.Attributes != null && crawledCafe.Attributes.Any())
                                existingCafe.SetAttributes(crawledCafe.Attributes);

                            updated++;
                        }
                        else
                        {
                            // Validate required fields
                            if (string.IsNullOrEmpty(crawledCafe.City) || !crawledCafe.Latitude.HasValue || !crawledCafe.Longitude.HasValue)
                                continue;

                            var slug = await GenerateUniqueSlugAsync(context, crawledCafe.Name, usedSlugs, cancellationToken);
                            usedSlugs.Add(slug);

                            var newCafe = new Cafe
                            {
                                Name = crawledCafe.Name,
                                Address = crawledCafe.Address,
                                City = crawledCafe.City,
                                State = crawledCafe.State,
                                Country = crawledCafe.Country ?? "United States",
                                Latitude = crawledCafe.Latitude.Value,
                                Longitude = crawledCafe.Longitude.Value,
                                Phone = crawledCafe.Phone,
                                Website = crawledCafe.Website,
                                GooglePlaceId = crawledCafe.GooglePlaceId,
                                GoogleMapsUrl = crawledCafe.GoogleMapsUrl,
                                LocalImagePath = crawledCafe.LocalImagePath,
                                BggUsername = crawledCafe.BggUsername,
                                AverageRating = crawledCafe.Rating.HasValue ? (decimal)crawledCafe.Rating.Value : null,
                                TotalReviews = crawledCafe.ReviewCount ?? 0,
                                PriceRange = crawledCafe.PriceLevel,
                                OpeningHours = crawledCafe.OpeningHours,
                                Slug = slug,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsActive = true,
                                IsVerified = false
                            };

                            if (crawledCafe.Attributes != null && crawledCafe.Attributes.Any())
                                newCafe.SetAttributes(crawledCafe.Attributes);

                            context.Cafes.Add(newCafe);
                            added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error saving cafe: {Name}", crawledCafe.Name);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);

                // Update history
                history.CompletedAt = DateTime.UtcNow;
                history.Status = "Success";
                history.CafesFound = crawledCafes.Count;
                history.CafesAdded = added;
                history.CafesUpdated = updated;
                await context.SaveChangesAsync(cancellationToken);

                return new CrawlResult
                {
                    Success = true,
                    CafesFound = crawledCafes.Count,
                    CafesAdded = added,
                    CafesUpdated = updated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling city: {City}", city.Name);

                // Update history with error
                history.CompletedAt = DateTime.UtcNow;
                history.Status = "Failed";
                history.ErrorMessage = ex.Message.Length > 2000 ? ex.Message.Substring(0, 2000) : ex.Message;
                await context.SaveChangesAsync(cancellationToken);

                return new CrawlResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<CrawlResult> CrawlCityAsync(City city, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await CrawlCityInternalAsync(city, context, cancellationToken);
        }

        public async Task<List<City>> GetNextCitiesToCrawlAsync(int count)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await GetNextCitiesToCrawlInternalAsync(context, count, default);
        }

        private async Task<List<City>> GetNextCitiesToCrawlInternalAsync(ApplicationDbContext context, int count, CancellationToken cancellationToken)
        {
            // Priority: Never crawled (CrawlCount = 0) first, then lowest CrawlCount
            return await context.Cities
                .Where(c => c.IsActive && (c.NextCrawlAt == null || c.NextCrawlAt <= DateTime.UtcNow))
                .OrderBy(c => c.CrawlCount)
                .ThenBy(c => c.LastCrawledAt ?? DateTime.MinValue)
                .Take(count)
                .ToListAsync(cancellationToken);
        }

        public async Task SeedCitiesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Check if cities already seeded
            if (await context.Cities.AnyAsync())
            {
                _logger.LogInformation("Cities already seeded");
                return;
            }

            var cities = new List<City>();

            // US Cities
            var usCities = new[]
            {
                "Seattle, WA", "Portland, OR", "San Francisco, CA", "Los Angeles, CA", "San Diego, CA",
                "Sacramento, CA", "San Jose, CA", "Oakland, CA", "Fresno, CA", "Long Beach, CA",
                "Anaheim, CA", "Irvine, CA", "Santa Ana, CA", "Riverside, CA", "Pasadena, CA",
                "Berkeley, CA", "Santa Barbara, CA", "Santa Cruz, CA", "Tacoma, WA", "Spokane, WA",
                "Eugene, OR", "Salem, OR", "Boise, ID", "Anchorage, AK", "Honolulu, HI",
                "Phoenix, AZ", "Tucson, AZ", "Mesa, AZ", "Scottsdale, AZ", "Las Vegas, NV",
                "Reno, NV", "Albuquerque, NM", "Santa Fe, NM", "El Paso, TX",
                "Denver, CO", "Colorado Springs, CO", "Boulder, CO", "Fort Collins, CO",
                "Salt Lake City, UT", "Provo, UT",
                "Austin, TX", "Houston, TX", "Dallas, TX", "San Antonio, TX", "Fort Worth, TX",
                "Arlington, TX", "Plano, TX", "Irving, TX", "Frisco, TX", "McKinney, TX",
                "Chicago, IL", "Detroit, MI", "Minneapolis, MN", "St. Paul, MN", "Milwaukee, WI",
                "Madison, WI", "Indianapolis, IN", "Columbus, OH", "Cleveland, OH", "Cincinnati, OH",
                "St. Louis, MO", "Kansas City, MO", "Omaha, NE", "Des Moines, IA", "Wichita, KS",
                "Ann Arbor, MI", "Grand Rapids, MI", "Bloomington, IN", "Champaign, IL",
                "New York, NY", "Brooklyn, NY", "Queens, NY", "Manhattan, NY", "Boston, MA",
                "Cambridge, MA", "Philadelphia, PA", "Pittsburgh, PA", "Baltimore, MD",
                "Washington, DC", "Newark, NJ", "Jersey City, NJ", "Providence, RI", "Hartford, CT",
                "New Haven, CT", "Buffalo, NY", "Rochester, NY", "Syracuse, NY", "Albany, NY",
                "Atlanta, GA", "Miami, FL", "Orlando, FL", "Tampa, FL", "Jacksonville, FL",
                "Charlotte, NC", "Raleigh, NC", "Durham, NC", "Nashville, TN", "Memphis, TN",
                "Knoxville, TN", "Louisville, KY", "Lexington, KY", "New Orleans, LA", "Baton Rouge, LA",
                "Birmingham, AL", "Charleston, SC", "Savannah, GA", "Richmond, VA", "Virginia Beach, VA",
                "Norfolk, VA", "Asheville, NC", "Greenville, SC", "Columbia, SC",
                "Oklahoma City, OK", "Tulsa, OK", "Little Rock, AR", "Fayetteville, AR",
                "Portland, ME", "Burlington, VT", "Manchester, NH", "Bozeman, MT", "Missoula, MT"
            };

            foreach (var cityName in usCities)
            {
                cities.Add(new City
                {
                    Name = cityName,
                    Country = "United States",
                    Region = "US",
                    MaxResults = 15,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // International Cities
            var intlCities = new (string Name, string Country, int MaxResults)[]
            {
                // Vietnam
                ("Hanoi", "Vietnam", 15), ("Ho Chi Minh", "Vietnam", 15), ("Da Nang", "Vietnam", 15),
                ("Hai Phong", "Vietnam", 10), ("Can Tho", "Vietnam", 10), ("Nha Trang", "Vietnam", 10), ("Hue", "Vietnam", 10),
                // Japan
                ("Tokyo", "Japan", 20), ("Osaka", "Japan", 20), ("Kyoto", "Japan", 15),
                ("Nagoya", "Japan", 15), ("Yokohama", "Japan", 15), ("Fukuoka", "Japan", 15),
                ("Sapporo", "Japan", 15), ("Kobe", "Japan", 15),
                // South Korea
                ("Seoul", "South Korea", 20), ("Busan", "South Korea", 15), ("Incheon", "South Korea", 15),
                ("Daegu", "South Korea", 15), ("Daejeon", "South Korea", 15),
                // China
                ("Shanghai", "China", 20), ("Beijing", "China", 20), ("Guangzhou", "China", 20),
                ("Shenzhen", "China", 20), ("Chengdu", "China", 15), ("Hangzhou", "China", 15),
                ("Nanjing", "China", 15), ("Wuhan", "China", 15), ("Xian", "China", 15),
                // Taiwan
                ("Taipei", "Taiwan", 20), ("Taichung", "Taiwan", 15), ("Kaohsiung", "Taiwan", 15), ("Tainan", "Taiwan", 15),
                // Hong Kong & Macau
                ("Hong Kong", "Hong Kong", 20), ("Macau", "Macau", 10),
                // Southeast Asia
                ("Singapore", "Singapore", 20), ("Bangkok", "Thailand", 20), ("Chiang Mai", "Thailand", 15),
                ("Phuket", "Thailand", 10), ("Kuala Lumpur", "Malaysia", 20), ("Penang", "Malaysia", 15),
                ("Johor Bahru", "Malaysia", 10), ("Jakarta", "Indonesia", 20), ("Bali", "Indonesia", 15),
                ("Surabaya", "Indonesia", 15), ("Bandung", "Indonesia", 15), ("Manila", "Philippines", 20), ("Cebu", "Philippines", 15),
                // South Asia
                ("Mumbai", "India", 20), ("Delhi", "India", 20), ("Bangalore", "India", 20),
                ("Hyderabad", "India", 15), ("Chennai", "India", 15), ("Kolkata", "India", 15), ("Pune", "India", 15),
                // UK & Ireland
                ("London", "UK", 20), ("Manchester", "UK", 15), ("Birmingham", "UK", 15),
                ("Edinburgh", "UK", 15), ("Glasgow", "UK", 15), ("Bristol", "UK", 15),
                ("Liverpool", "UK", 15), ("Leeds", "UK", 15), ("Dublin", "Ireland", 15),
                // Germany
                ("Berlin", "Germany", 20), ("Munich", "Germany", 20), ("Hamburg", "Germany", 15),
                ("Frankfurt", "Germany", 15), ("Cologne", "Germany", 15), ("Dusseldorf", "Germany", 15),
                ("Stuttgart", "Germany", 15), ("Essen", "Germany", 15),
                // France
                ("Paris", "France", 20), ("Lyon", "France", 15), ("Marseille", "France", 15),
                ("Toulouse", "France", 15), ("Nice", "France", 15), ("Bordeaux", "France", 15),
                // Spain
                ("Madrid", "Spain", 20), ("Barcelona", "Spain", 20), ("Valencia", "Spain", 15),
                ("Seville", "Spain", 15), ("Bilbao", "Spain", 15),
                // Italy
                ("Rome", "Italy", 20), ("Milan", "Italy", 20), ("Florence", "Italy", 15),
                ("Naples", "Italy", 15), ("Turin", "Italy", 15), ("Bologna", "Italy", 15),
                // Netherlands & Belgium
                ("Amsterdam", "Netherlands", 20), ("Rotterdam", "Netherlands", 15), ("The Hague", "Netherlands", 15),
                ("Utrecht", "Netherlands", 15), ("Brussels", "Belgium", 15), ("Antwerp", "Belgium", 15),
                // Scandinavia
                ("Stockholm", "Sweden", 15), ("Gothenburg", "Sweden", 15), ("Copenhagen", "Denmark", 15),
                ("Oslo", "Norway", 15), ("Helsinki", "Finland", 15),
                // Central & Eastern Europe
                ("Prague", "Czech Republic", 20), ("Vienna", "Austria", 15), ("Zurich", "Switzerland", 15),
                ("Geneva", "Switzerland", 15), ("Warsaw", "Poland", 15), ("Krakow", "Poland", 15),
                ("Budapest", "Hungary", 15), ("Bucharest", "Romania", 15),
                // Portugal & Greece
                ("Lisbon", "Portugal", 15), ("Porto", "Portugal", 15), ("Athens", "Greece", 15),
                // Canada
                ("Toronto", "Canada", 20), ("Vancouver", "Canada", 20), ("Montreal", "Canada", 20),
                ("Calgary", "Canada", 15), ("Edmonton", "Canada", 15), ("Ottawa", "Canada", 15),
                ("Quebec City", "Canada", 15), ("Winnipeg", "Canada", 15),
                // Australia & New Zealand
                ("Sydney", "Australia", 20), ("Melbourne", "Australia", 20), ("Brisbane", "Australia", 15),
                ("Perth", "Australia", 15), ("Adelaide", "Australia", 15), ("Auckland", "New Zealand", 15), ("Wellington", "New Zealand", 15),
                // Latin America
                ("Mexico City", "Mexico", 20), ("Guadalajara", "Mexico", 15), ("Monterrey", "Mexico", 15),
                ("Sao Paulo", "Brazil", 20), ("Rio de Janeiro", "Brazil", 20), ("Curitiba", "Brazil", 15),
                ("Belo Horizonte", "Brazil", 15), ("Buenos Aires", "Argentina", 20), ("Santiago", "Chile", 15),
                ("Lima", "Peru", 15), ("Bogota", "Colombia", 15), ("Medellin", "Colombia", 15),
                // Middle East
                ("Dubai", "UAE", 15), ("Abu Dhabi", "UAE", 15), ("Tel Aviv", "Israel", 15),
                ("Istanbul", "Turkey", 20), ("Ankara", "Turkey", 15),
                // Africa
                ("Cape Town", "South Africa", 15), ("Johannesburg", "South Africa", 15),
                ("Cairo", "Egypt", 15), ("Nairobi", "Kenya", 10), ("Lagos", "Nigeria", 10)
            };

            foreach (var (name, country, maxResults) in intlCities)
            {
                cities.Add(new City
                {
                    Name = name,
                    Country = country,
                    Region = "International",
                    MaxResults = maxResults,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            context.Cities.AddRange(cities);
            await context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} cities", cities.Count);
        }

        private async Task<string> GenerateUniqueSlugAsync(ApplicationDbContext context, string name, HashSet<string> usedSlugs, CancellationToken cancellationToken)
        {
            var baseSlug = GenerateSlug(name);
            var slug = baseSlug;
            var counter = 1;

            while (usedSlugs.Contains(slug) || await context.Cafes.AnyAsync(c => c.Slug == slug, cancellationToken))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Guid.NewGuid().ToString("N").Substring(0, 8);

            var slug = name.ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (string.IsNullOrEmpty(slug))
                slug = Guid.NewGuid().ToString("N").Substring(0, 8);

            return slug.Length > 100 ? slug.Substring(0, 100) : slug;
        }
    }
}
