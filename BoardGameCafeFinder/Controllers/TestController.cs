using BoardGameCafeFinder.Models.DTOs;
using BoardGameCafeFinder.Services;
using BoardGameCafeFinder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Controllers
{
    public class CrawlRequest
    {
        public string Location { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 20;
        public bool UseMultiQuery { get; set; } = false;
        public string[]? Queries { get; set; }
    }
    /// <summary>
    /// Test controller for testing features without Google Maps API key
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("Test")]
    public class TestController : Controller
    {
        private readonly ICafeService _cafeService;
        private readonly IGoogleMapsCrawlerService _crawlerService;
        private readonly IBggSyncService _bggSyncService;
        private readonly IBggXmlApiService _bggXmlApiService;
        private readonly ICafeWebsiteCrawlerService _cafeWebsiteCrawlerService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestController> _logger;
        private readonly IWebHostEnvironment _environment;

        public TestController(
            ICafeService cafeService,
            IGoogleMapsCrawlerService crawlerService,
            IBggSyncService bggSyncService,
            IBggXmlApiService bggXmlApiService,
            ICafeWebsiteCrawlerService cafeWebsiteCrawlerService,
            ApplicationDbContext context,
            ILogger<TestController> logger,
            IWebHostEnvironment environment)
        {
            _cafeService = cafeService;
            _crawlerService = crawlerService;
            _bggSyncService = bggSyncService;
            _bggXmlApiService = bggXmlApiService;
            _cafeWebsiteCrawlerService = cafeWebsiteCrawlerService;
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Test page to see all cafés without map
        /// GET: /Test/Cafes
        /// </summary>
        [Route("Cafes")]
        public async Task<IActionResult> Cafes(string? search, string? country, string? city, int page = 1, int pageSize = 50)
        {
            // Get all cafes from database with filtering
            var query = _context.Cafes
                .Include(c => c.CafeGames)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Name.Contains(search) || (c.Address != null && c.Address.Contains(search)));
            }

            if (!string.IsNullOrEmpty(country))
            {
                query = query.Where(c => c.Country == country);
            }

            if (!string.IsNullOrEmpty(city))
            {
                query = query.Where(c => c.City == city);
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Get paginated results
            var cafes = await query
                .OrderBy(c => c.City)
                .ThenBy(c => c.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Convert to DTOs
            var cafeDtos = cafes.Select(c => new CafeSearchResultDto
            {
                Id = c.CafeId,
                Name = c.Name,
                Address = c.Address ?? "",
                City = c.City ?? "",
                State = c.State ?? "",
                Phone = c.Phone,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                AverageRating = c.AverageRating,
                TotalReviews = c.TotalReviews,
                TotalGames = c.CafeGames?.Count ?? 0,
                IsPremium = c.IsPremium,
                IsVerified = c.IsVerified,
                IsOpenNow = false,
                Website = c.Website
            }).ToList();

            // Get filter options
            var countries = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.Country))
                .Select(c => c.Country!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var cities = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.City))
                .Select(c => c.City!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // If country is selected, filter cities
            if (!string.IsNullOrEmpty(country))
            {
                cities = await _context.Cafes
                    .Where(c => c.Country == country && !string.IsNullOrEmpty(c.City))
                    .Select(c => c.City!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();
            }

            ViewBag.Countries = countries;
            ViewBag.Cities = cities;
            ViewBag.SelectedCountry = country;
            ViewBag.SelectedCity = city;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(cafeDtos);
        }

        /// <summary>
        /// Find board games for a specific cafe - uses BGG sync if BggUsername exists, otherwise crawls website
        /// POST: /Test/FindGamesForCafe/{cafeId}
        /// </summary>
        [HttpPost("[action]/{cafeId}")]
        public async Task<IActionResult> FindGamesForCafe(int cafeId)
        {
            try
            {
                // Get cafe from database
                var cafe = await _context.Cafes.FindAsync(cafeId);
                if (cafe == null)
                {
                    return Json(new { success = false, message = "Cafe not found" });
                }

                // If cafe has BggUsername, use BGG sync instead of website crawling
                if (!string.IsNullOrEmpty(cafe.BggUsername))
                {
                    _logger.LogInformation("Syncing games for cafe {CafeName} (ID: {CafeId}) from BGG username: {BggUsername}",
                        cafe.Name, cafeId, cafe.BggUsername);

                    var syncResult = await _bggSyncService.SyncCafeGamesAsync(cafeId);

                    return Json(new
                    {
                        success = syncResult.Success,
                        message = syncResult.Success
                            ? $"Synced {syncResult.GamesAdded} games from BGG (updated: {syncResult.GamesUpdated})"
                            : syncResult.Message,
                        gamesFound = syncResult.GamesAdded + syncResult.GamesUpdated,
                        source = "bgg"
                    });
                }

                // Check if cafe has a website for crawling
                if (string.IsNullOrEmpty(cafe.Website))
                {
                    return Json(new { success = false, message = "Cafe has no BGG username or website URL" });
                }

                _logger.LogInformation("Finding games for cafe {CafeName} (ID: {CafeId}) from website: {Website}",
                    cafe.Name, cafeId, cafe.Website);

                // Crawl website for games
                var crawledGames = await _cafeWebsiteCrawlerService.CrawlCafeWebsiteForGamesAsync(cafe.Website);

                if (crawledGames == null || !crawledGames.Any())
                {
                    return Json(new { success = false, message = "No games found on cafe website", gamesFound = 0 });
                }

                // Try to match with BGG and enrich data
                foreach (var game in crawledGames)
                {
                    try
                    {
                        var searchResults = await _bggXmlApiService.SearchGamesAsync(game.Name);
                        var match = searchResults.FirstOrDefault(r => r.Name.Equals(game.Name, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                        {
                            game.BggId = match.BggId;

                            var details = await _bggXmlApiService.GetGameDetailsAsync(match.BggId);
                            if (details != null)
                            {
                                if (string.IsNullOrEmpty(game.ImageUrl)) game.ImageUrl = details.ThumbnailUrl ?? details.ImageUrl;
                                if (string.IsNullOrEmpty(game.Description)) game.Description = details.Description;
                                if (game.MinPlayers == null) game.MinPlayers = details.MinPlayers;
                                if (game.MaxPlayers == null) game.MaxPlayers = details.MaxPlayers;
                                if (game.PlaytimeMinutes == null) game.PlaytimeMinutes = details.PlayingTime;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error matching game {GameName} with BGG", game.Name);
                    }
                }

                // Save games to database
                await SaveFoundGamesAsync(cafeId, crawledGames);

                _logger.LogInformation("Successfully found and saved {Count} games for cafe {CafeName}",
                    crawledGames.Count, cafe.Name);

                return Json(new
                {
                    success = true,
                    message = $"Found {crawledGames.Count} games from website",
                    gamesFound = crawledGames.Count,
                    games = crawledGames.Select(g => new { g.Name, g.BggId, g.Price }).ToList(),
                    source = "website"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding games for cafe {CafeId}", cafeId);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }


        /// <summary>
        /// Test API endpoints directly
        /// GET: /Test/Api
        /// </summary>
        [Route("Api")]
        public IActionResult Api()
        {
            return View();
        }

        /// <summary>
        /// Test geospatial distance calculations
        /// GET: /Test/Distance
        /// </summary>
        [Route("Distance")]
        public IActionResult Distance()
        {
            var seattle = (lat: 47.6062, lon: -122.3321, name: "Seattle");
            var portland = (lat: 45.5152, lon: -122.6784, name: "Portland");
            var chicago = (lat: 41.8781, lon: -87.6298, name: "Chicago");
            var newYork = (lat: 40.7128, lon: -74.0060, name: "New York");
            var losAngeles = (lat: 34.0522, lon: -118.2437, name: "Los Angeles");

            var distances = new
            {
                SeattleToPortland = new
                {
                    Distance = CalculateDistance(seattle.lat, seattle.lon, portland.lat, portland.lon),
                    From = seattle.name,
                    To = portland.name
                },
                SeattleToChicago = new
                {
                    Distance = CalculateDistance(seattle.lat, seattle.lon, chicago.lat, chicago.lon),
                    From = seattle.name,
                    To = chicago.name
                },
                PortlandToChicago = new
                {
                    Distance = CalculateDistance(portland.lat, portland.lon, chicago.lat, chicago.lon),
                    From = portland.name,
                    To = chicago.name
                },
                NewYorkToLosAngeles = new
                {
                    Distance = CalculateDistance(newYork.lat, newYork.lon, losAngeles.lat, losAngeles.lon),
                    From = newYork.name,
                    To = losAngeles.name
                }
            };

            return Json(distances);
        }

        /// <summary>
        /// Test database and seeding
        /// GET: /Test/Database
        /// </summary>
        [Route("Database")]
        public async Task<IActionResult> Database()
        {
            var allCafes = await _cafeService.GetByCityAsync("Seattle");

            var info = new
            {
                TotalCafes = allCafes.Count,
                Cities = allCafes.Select(c => c.City).Distinct().ToList(),
                Cafes = allCafes.Select(c => new
                {
                    c.CafeId,
                    c.Name,
                    c.City,
                    c.State,
                    c.Address,
                    c.Latitude,
                    c.Longitude,
                    c.AverageRating,
                    c.TotalReviews,
                    c.IsPremium,
                    c.IsVerified
                }).ToList()
            };

            return Json(info);
        }

        /// <summary>
        /// Crawl board game cafes from Google Maps
        /// GET: /Test/Crawl?location=Seattle&maxResults=10
        /// </summary>
        [Route("Crawl")]
        public async Task<IActionResult> Crawl(string location = "Seattle", int maxResults = 10)
        {
            _logger.LogInformation("Starting crawl for location: {Location}, maxResults: {MaxResults}", location, maxResults);

            var results = await _crawlerService.CrawlBoardGameCafesAsync(location, maxResults);

            return Json(new
            {
                Location = location,
                TotalFound = results.Count,
                Cafes = results.Select(c => new
                {
                    c.Name,
                    c.Address,
                    c.Phone,
                    c.Website,
                    c.Rating,
                    c.ReviewCount,
                    c.PriceLevel,
                    c.Latitude,
                    c.Longitude,
                    c.Categories,
                    c.GoogleMapsUrl,
                    c.GooglePlaceId,
                    c.City,
                    c.State,
                    c.Country,
                    c.Attributes
                })
            });
        }

        /// <summary>
        /// Crawl and save board game cafes to database
        /// POST: /Test/CrawlAndSave
        /// </summary>
        [Route("CrawlAndSave")]
        [HttpPost]
        public async Task<IActionResult> CrawlAndSave([FromBody] CrawlRequest request)
        {
            _logger.LogInformation("Starting CrawlAndSave for location: {Location}, UseMultiQuery: {UseMultiQuery}",
                request.Location, request.UseMultiQuery);

            List<CrawledCafeData> results;

            if (request.UseMultiQuery && request.Queries != null && request.Queries.Length > 0)
            {
                // Use multiple search queries for better coverage
                results = await _crawlerService.CrawlWithMultipleQueriesAsync(
                    request.Location,
                    request.Queries,
                    request.MaxResults / request.Queries.Length + 1);
            }
            else
            {
                results = await _crawlerService.CrawlBoardGameCafesAsync(request.Location, request.MaxResults);
            }

            int added = 0;
            int updated = 0;
            int skipped = 0;
            int reviewsAdded = 0;
            var errors = new List<string>();
            var usedSlugs = new HashSet<string>();

            foreach (var crawledCafe in results)
            {
                try
                {
                    // Check if cafe already exists by GooglePlaceId
                    var existingCafe = await _context.Cafes
                        .FirstOrDefaultAsync(c => c.GooglePlaceId == crawledCafe.GooglePlaceId && crawledCafe.GooglePlaceId != null);

                    if (existingCafe != null)
                    {
                        // Update existing cafe
                        existingCafe.Name = crawledCafe.Name;
                        existingCafe.Address = crawledCafe.Address;
                        existingCafe.City = crawledCafe.City ?? existingCafe.City;
                        existingCafe.State = crawledCafe.State ?? existingCafe.State;
                        existingCafe.Country = crawledCafe.Country ?? "United States";
                        existingCafe.Phone = crawledCafe.Phone;
                        existingCafe.Website = crawledCafe.Website;
                        existingCafe.Latitude = crawledCafe.Latitude ?? existingCafe.Latitude;
                        existingCafe.Longitude = crawledCafe.Longitude ?? existingCafe.Longitude;
                        existingCafe.AverageRating = crawledCafe.Rating.HasValue ? (decimal)crawledCafe.Rating.Value : existingCafe.AverageRating;
                        if (crawledCafe.ReviewCount.HasValue)
                        {
                            existingCafe.TotalReviews = crawledCafe.ReviewCount.Value;
                        }
                        existingCafe.PriceRange = crawledCafe.PriceLevel;
                        existingCafe.GoogleMapsUrl = crawledCafe.GoogleMapsUrl;
                        existingCafe.LocalImagePath = crawledCafe.LocalImagePath;
                        existingCafe.BggUsername = crawledCafe.BggUsername ?? existingCafe.BggUsername;
                        existingCafe.UpdatedAt = DateTime.UtcNow;

                        if (crawledCafe.Attributes != null && crawledCafe.Attributes.Any())
                        {
                            existingCafe.SetAttributes(crawledCafe.Attributes);
                        }

                        updated++;

                        // Add reviews for existing cafe
                        if (crawledCafe.Reviews.Any())
                        {
                            var convertedReviews = _crawlerService.ConvertToReviews(existingCafe.CafeId, crawledCafe.Reviews);
                            var newReviews = await FilterDuplicateReviewsAsync(existingCafe.CafeId, convertedReviews);
                            if (newReviews.Any())
                            {
                                _context.Reviews.AddRange(newReviews);
                                reviewsAdded += newReviews.Count;
                            }
                        }

                        // Save photos for existing cafe (only those with local paths)
                        if (crawledCafe.PhotoUrls.Any() && crawledCafe.PhotoLocalPaths.Any())
                        {
                             await SaveCafePhotosAsync(existingCafe.CafeId, crawledCafe.PhotoUrls, crawledCafe.PhotoLocalPaths);
                        }

                        // Save found games for existing cafe
                        if (crawledCafe.FoundGames.Any())
                        {
                            await SaveFoundGamesAsync(existingCafe.CafeId, crawledCafe.FoundGames);
                        }
                    }
                    else
                    {
                        // Validate required fields
                        if (string.IsNullOrEmpty(crawledCafe.City) || !crawledCafe.Latitude.HasValue || !crawledCafe.Longitude.HasValue)
                        {
                            _logger.LogWarning("Skipping cafe {Name} - missing required fields", crawledCafe.Name);
                            skipped++;
                            errors.Add($"{crawledCafe.Name}: Missing city or coordinates");
                            continue;
                        }

                        // Create new cafe
                        var slug = await GenerateUniqueSlugAsync(crawledCafe.Name, usedSlugs);
                        usedSlugs.Add(slug);

                        var newCafe = new BoardGameCafeFinder.Models.Domain.Cafe
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
                        {
                            newCafe.SetAttributes(crawledCafe.Attributes);
                        }

                        _context.Cafes.Add(newCafe);
                        added++;

                        // Save cafe first to get CafeId, then add reviews
                        await _context.SaveChangesAsync();

                        if (crawledCafe.Reviews.Any())
                        {
                            var convertedReviews = _crawlerService.ConvertToReviews(newCafe.CafeId, crawledCafe.Reviews);
                            _context.Reviews.AddRange(convertedReviews);
                            reviewsAdded += convertedReviews.Count;
                        }

                        // Save photos for new cafe (only those with local paths)
                        if (crawledCafe.PhotoUrls.Count > 0 && crawledCafe.PhotoLocalPaths.Count > 0)
                        {
                             await SaveCafePhotosAsync(newCafe.CafeId, crawledCafe.PhotoUrls, crawledCafe.PhotoLocalPaths);
                        }

                        // Save found games for new cafe
                        if (crawledCafe.FoundGames.Any())
                        {
                            await SaveFoundGamesAsync(newCafe.CafeId, crawledCafe.FoundGames);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving cafe: {Name}", crawledCafe.Name);
                    errors.Add($"{crawledCafe.Name}: {ex.Message}");
                    skipped++;
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                Success = true,
                Location = request.Location,
                TotalCrawled = results.Count,
                Added = added,
                Updated = updated,
                Skipped = skipped,
                ReviewsAdded = reviewsAdded,
                Errors = errors
            });
        }

        /// <summary>
        /// Sync games for a cafe from BGG
        /// GET: /Test/SyncBgg?cafeId=1
        /// </summary>
        [Route("SyncBgg")]
        public async Task<IActionResult> SyncBgg(int cafeId)
        {
            var result = await _bggSyncService.SyncCafeGamesAsync(cafeId);
            return Json(result);
        }

        /// <summary>
        /// Sync games for a cafe from BGG using XML API (faster, no scraping)
        /// GET: /Test/SyncBggApi?cafeId=1
        /// </summary>
        [Route("SyncBggApi")]
        public async Task<IActionResult> SyncBggApi(int cafeId)
        {
            var result = await _bggXmlApiService.SyncCafeGamesViaApiAsync(cafeId);
            return Json(result);
        }

        /// <summary>
        /// Get BGG user collection via XML API
        /// GET: /Test/BggCollection?username=moxboardinghouse
        /// </summary>
        [Route("BggCollection")]
        public async Task<IActionResult> BggCollection(string username)
        {
            var games = await _bggXmlApiService.GetUserCollectionAsync(username);
            return Json(new
            {
                Username = username,
                TotalGames = games.Count,
                Games = games.Take(50).Select(g => new
                {
                    g.BggId,
                    g.Name,
                    g.YearPublished,
                    g.MinPlayers,
                    g.MaxPlayers,
                    g.PlayingTime,
                    g.Rating,
                    g.ThumbnailUrl
                })
            });
        }

        /// <summary>
        /// Search for board games on BGG via XML API
        /// GET: /Test/BggSearch?query=Catan
        /// </summary>
        [Route("BggSearch")]
        public async Task<IActionResult> BggSearch(string query)
        {
            var games = await _bggXmlApiService.SearchGamesAsync(query);
            return Json(new
            {
                Query = query,
                TotalResults = games.Count,
                Games = games.Take(20).Select(g => new
                {
                    g.BggId,
                    g.Name,
                    g.YearPublished
                })
            });
        }

        /// <summary>
        /// Get detailed game info from BGG via XML API
        /// GET: /Test/BggGame?bggId=13
        /// </summary>
        [Route("BggGame")]
        public async Task<IActionResult> BggGame(int bggId)
        {
            var game = await _bggXmlApiService.GetGameDetailsAsync(bggId);
            if (game == null)
            {
                return NotFound("Game not found");
            }
            return Json(game);
        }

        /// <summary>
        /// Link a BGG username to a cafe
        /// GET: /Test/LinkBgg?cafeId=1&bggUsername=moxboardinghouse
        /// </summary>
        [Route("LinkBgg")]
        public async Task<IActionResult> LinkBgg(int cafeId, string bggUsername)
        {
            var cafe = await _context.Cafes.FindAsync(cafeId);
            if (cafe == null) return NotFound("Cafe not found");

            cafe.BggUsername = bggUsername;
            cafe.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { Success = true, Cafe = cafe.Name, BggUsername = bggUsername });
        }

        /// <summary>
        /// Auto-discover BGG username for a cafe and sync games
        /// GET: /Test/AutoDiscoverBgg?cafeId=1
        /// </summary>
        [Route("AutoDiscoverBgg")]
        public async Task<IActionResult> AutoDiscoverBgg(int cafeId)
        {
            var result = await _bggXmlApiService.AutoDiscoverAndSyncAsync(cafeId);
            var cafe = await _context.Cafes.FindAsync(cafeId);
            return Json(new
            {
                CafeId = cafeId,
                CafeName = cafe?.Name,
                DiscoveredUsername = cafe?.BggUsername,
                SyncResult = result
            });
        }

        /// <summary>
        /// Find possible BGG usernames for a cafe name (without saving)
        /// GET: /Test/FindBggUsernames?cafeName=Mox Boarding House
        /// </summary>
        [Route("FindBggUsernames")]
        public async Task<IActionResult> FindBggUsernames(string cafeName)
        {
            var usernames = await _bggXmlApiService.FindPossibleBggUsernamesAsync(cafeName);
            return Json(new
            {
                CafeName = cafeName,
                PossibleUsernames = usernames
            });
        }

        /// <summary>
        /// Sync all cafes with auto-discover for missing BGG usernames
        /// POST: /Test/SyncAllBgg
        /// </summary>
        [Route("SyncAllBgg")]
        [HttpPost]
        public async Task<IActionResult> SyncAllBgg()
        {
            var results = await _bggXmlApiService.SyncAllCafesWithAutoDiscoverAsync();
            return Json(new
            {
                TotalCafes = results.Count,
                SuccessfulSyncs = results.Count(r => r.Result.Success),
                FailedSyncs = results.Count(r => !r.Result.Success),
                TotalGamesAdded = results.Sum(r => r.Result.GamesAdded),
                TotalGamesUpdated = results.Sum(r => r.Result.GamesUpdated),
                Results = results.Select(r => new
                {
                    r.CafeId,
                    r.CafeName,
                    r.Result.Success,
                    r.Result.Message,
                    r.Result.GamesAdded,
                    r.Result.GamesUpdated,
                    r.Result.GamesProcessed
                })
            });
        }

        /// <summary>
        /// View with buttons to crawl major cities
        /// GET: /Test/CrawlManager
        /// </summary>
        [Route("CrawlManager")]
        public IActionResult CrawlManager()
        {
            return View();
        }

        /// <summary>
        /// Clean all phone numbers in database by removing special/control characters
        /// POST: /Test/CleanPhoneNumbers
        /// </summary>
        [HttpPost]
        [Route("CleanPhoneNumbers")]
        public async Task<IActionResult> CleanPhoneNumbers()
        {
            try
            {
                var cafes = await _context.Cafes.Where(c => !string.IsNullOrEmpty(c.Phone)).ToListAsync();
                int updated = 0;

                foreach (var cafe in cafes)
                {
                    var originalPhone = cafe.Phone ?? "";
                    // Keep only ASCII printable characters (0-9, +, -, spaces, parentheses)
                    var cleanPhone = System.Text.RegularExpressions.Regex.Replace(originalPhone,
                        @"[^\d\+\-\s\(\)]", "").Trim();
                    // Clean up multiple spaces
                    cleanPhone = System.Text.RegularExpressions.Regex.Replace(cleanPhone, @"\s+", " ");

                    if (cleanPhone != originalPhone)
                    {
                        cafe.Phone = cleanPhone;
                        updated++;
                        _logger.LogInformation("Cleaned phone for {CafeName}: '{Original}' -> '{Cleaned}'",
                            cafe.Name, originalPhone, cleanPhone);
                    }
                }

                if (updated > 0)
                {
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    Success = true,
                    Message = $"Cleaned {updated} phone numbers",
                    CafesProcessed = cafes.Count,
                    Updated = updated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning phone numbers");
                return Json(new
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Clear all cafe data from database
        /// POST: /Test/ClearAllData
        /// </summary>
        [Route("ClearAllData")]
        [HttpPost]
        public async Task<IActionResult> ClearAllData(bool preserveBoardGames = true)
        {
            try
            {
                // Get counts before deletion
                var cafeCount = await _context.Cafes.CountAsync();
                var reviewCount = await _context.Reviews.CountAsync();
                var photoCount = await _context.Photos.CountAsync();
                var eventCount = await _context.Events.CountAsync();
                var cafeGameCount = await _context.CafeGames.CountAsync();
                var premiumListingCount = await _context.PremiumListings.CountAsync();
                var boardGameCount = await _context.BoardGames.CountAsync();

                // Delete in order (related tables first, then Cafes)
                // Due to CASCADE delete, we only need to delete Cafes
                // But we can also explicitly delete related data first for clarity

                _context.EventBookings.RemoveRange(_context.EventBookings);
                _context.Events.RemoveRange(_context.Events);
                _context.Reviews.RemoveRange(_context.Reviews);
                _context.Photos.RemoveRange(_context.Photos);
                _context.CafeGames.RemoveRange(_context.CafeGames);
                _context.PremiumListings.RemoveRange(_context.PremiumListings);
                _context.Cafes.RemoveRange(_context.Cafes);

                // Optionally delete BoardGames (default: preserve them for whitelist matching)
                int boardGamesDeleted = 0;
                if (!preserveBoardGames)
                {
                    _context.BoardGames.RemoveRange(_context.BoardGames);
                    boardGamesDeleted = boardGameCount;
                }

                await _context.SaveChangesAsync();

                // Delete all images from wwwroot/images/cafes folder
                int imagesDeleted = 0;
                var cafesImagePath = Path.Combine(_environment.WebRootPath, "images", "cafes");
                if (Directory.Exists(cafesImagePath))
                {
                    var imageFiles = Directory.GetFiles(cafesImagePath);
                    foreach (var file in imageFiles)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            imagesDeleted++;
                        }
                        catch (Exception fileEx)
                        {
                            _logger.LogWarning(fileEx, "Failed to delete image file: {File}", file);
                        }
                    }
                    _logger.LogInformation("Deleted {Count} image files from {Path}", imagesDeleted, cafesImagePath);
                }

                _logger.LogInformation("Cleared all data: {CafeCount} cafes, {ReviewCount} reviews, {PhotoCount} photos, {EventCount} events, {CafeGameCount} cafe-games, {PremiumListingCount} premium listings, {BoardGamesDeleted} board games, {ImagesDeleted} image files (preserveBoardGames: {PreserveBoardGames})",
                    cafeCount, reviewCount, photoCount, eventCount, cafeGameCount, premiumListingCount, boardGamesDeleted, imagesDeleted, preserveBoardGames);

                return Json(new
                {
                    success = true,
                    message = preserveBoardGames
                        ? $"All cafe data cleared. Board games preserved ({boardGameCount} games in whitelist)."
                        : "All data cleared including board games.",
                    deleted = new
                    {
                        cafes = cafeCount,
                        reviews = reviewCount,
                        photos = photoCount,
                        events = eventCount,
                        cafeGames = cafeGameCount,
                        premiumListings = premiumListingCount,
                        boardGames = boardGamesDeleted,
                        imageFiles = imagesDeleted
                    },
                    boardGamesPreserved = preserveBoardGames ? boardGameCount : 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all data");
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// POST: /Test/SeedBoardGames
        /// Seed board games from BGG for whitelist matching
        /// </summary>
        [Route("SeedBoardGames")]
        [HttpPost]
        public async Task<IActionResult> SeedBoardGames(int count = 200)
        {
            try
            {
                var existingCount = await _context.BoardGames.CountAsync();
                _logger.LogInformation("Starting board game seeding. Existing: {Existing}, Target: {Target}", existingCount, count);

                var added = await _bggXmlApiService.SeedBoardGamesFromBggAsync(count);

                var totalCount = await _context.BoardGames.CountAsync();

                return Json(new
                {
                    success = true,
                    message = $"Board game seeding completed. Added {added} new games.",
                    existingBefore = existingCount,
                    added = added,
                    totalNow = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding board games");
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// GET: /Test/BoardGamesCount
        /// Get current board games count in database
        /// </summary>
        [Route("BoardGamesCount")]
        [HttpGet]
        public async Task<IActionResult> BoardGamesCount()
        {
            var total = await _context.BoardGames.CountAsync();
            var withBggId = await _context.BoardGames.CountAsync(g => g.BGGId.HasValue);

            return Json(new
            {
                total = total,
                withBggId = withBggId,
                withoutBggId = total - withBggId
            });
        }

        private async Task<string> GenerateUniqueSlugAsync(string name, HashSet<string>? usedSlugs = null)
        {
            var baseSlug = GenerateSlug(name);
            var slug = baseSlug;
            var counter = 1;

            // Check both database and in-memory used slugs
            while (await _context.Cafes.AnyAsync(c => c.Slug == slug) ||
                   (usedSlugs != null && usedSlugs.Contains(slug)))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in kilometers

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return Math.Round(R * c, 2); // Return in kilometers, rounded to 2 decimals
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private async Task<List<BoardGameCafeFinder.Models.Domain.Review>> FilterDuplicateReviewsAsync(int cafeId, List<BoardGameCafeFinder.Models.Domain.Review> reviews)
        {
            var existingReviewTexts = await _context.Reviews
                .Where(r => r.CafeId == cafeId)
                .Select(r => r.Content)
                .ToListAsync();

            return reviews
                .Where(r => !existingReviewTexts.Contains(r.Content))
                .ToList();
        }

        private async Task SaveCafePhotosAsync(int cafeId, List<string> photoUrls, List<string> photoLocalPaths)
        {
            // Only save photos that have been successfully downloaded locally
            if (photoUrls.Count != photoLocalPaths.Count)
            {
                _logger.LogWarning("Photo URLs and local paths count mismatch for cafe {CafeId}", cafeId);
                return;
            }

            var existingLocalPaths = await _context.Photos
                .Where(p => p.CafeId == cafeId && p.LocalPath != null)
                .Select(p => p.LocalPath)
                .ToListAsync();

            var newPhotos = new List<BoardGameCafeFinder.Models.Domain.Photo>();
            int order = await _context.Photos.Where(p => p.CafeId == cafeId).CountAsync();

            for (int i = 0; i < photoUrls.Count; i++)
            {
                var localPath = photoLocalPaths[i];

                // Skip if already exists (by local path)
                if (existingLocalPaths.Contains(localPath))
                {
                    continue;
                }

                newPhotos.Add(new BoardGameCafeFinder.Models.Domain.Photo
                {
                    CafeId = cafeId,
                    Url = localPath, // Use local path as the primary URL
                    LocalPath = localPath,
                    UploadedAt = DateTime.UtcNow,
                    IsApproved = true,
                    DisplayOrder = order++
                });
            }

            if (newPhotos.Count > 0)
            {
                _context.Photos.AddRange(newPhotos);
                _logger.LogInformation("Added {Count} photos for cafe {CafeId}", newPhotos.Count, cafeId);
            }
        }

        private async Task SaveFoundGamesAsync(int cafeId, List<CrawledGameData> foundGames)
        {
            if (foundGames == null || !foundGames.Any()) return;

            // Filter out non-game items (gift cards, accessories, etc.)
            var nonGameKeywords = new[] { "gift card", "gift certificate", "voucher", "coupon", "sleeve", "dice bag", "playmat", "card holder", "token", "accessory", "merchandise", "t-shirt", "shirt", "mug", "poster" };
            foundGames = foundGames.Where(g => !nonGameKeywords.Any(k => g.Name.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

            int added = 0;
            foreach (var gameData in foundGames)
            {
                try
                {
                    // Truncate description if too long (max 4000 chars)
                    if (!string.IsNullOrEmpty(gameData.Description) && gameData.Description.Length > 4000)
                    {
                        gameData.Description = gameData.Description[..3997] + "...";
                    }

                    // 1. Find or Create BoardGame
                    BoardGame? game = null;
                    if (gameData.BggId.HasValue)
                    {
                        game = await _context.BoardGames.FirstOrDefaultAsync(g => g.BGGId == gameData.BggId);
                    }

                    if (game == null)
                    {
                        game = await _context.BoardGames.FirstOrDefaultAsync(g => g.Name == gameData.Name);
                    }

                    if (game == null)
                    {
                        game = new BoardGame
                        {
                            Name = gameData.Name,
                            BGGId = gameData.BggId,
                            SourceUrl = gameData.SourceUrl,
                            ImageUrl = gameData.ImageUrl,
                            Description = gameData.Description,
                            MinPlayers = gameData.MinPlayers,
                            MaxPlayers = gameData.MaxPlayers,
                            PlaytimeMinutes = gameData.PlaytimeMinutes,
                            Price = gameData.Price,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.BoardGames.Add(game);
                        await _context.SaveChangesAsync();
                        added++;
                    }
                    else
                    {
                        // Update specific fields if they are missing/better
                        bool changed = false;
                        if (string.IsNullOrEmpty(game.SourceUrl) && !string.IsNullOrEmpty(gameData.SourceUrl)) { game.SourceUrl = gameData.SourceUrl; changed = true; }
                        if (!game.BGGId.HasValue && gameData.BggId.HasValue) { game.BGGId = gameData.BggId; changed = true; }
                        if (!game.Price.HasValue && gameData.Price.HasValue) { game.Price = gameData.Price; changed = true; }
                        
                        if (changed) await _context.SaveChangesAsync();
                    }

                    // 2. Link to Cafe
                    var cafeGame = await _context.CafeGames.FirstOrDefaultAsync(x=>x.CafeId == cafeId && x.GameId == game.GameId);
                    if (cafeGame == null)
                    {
                        _context.CafeGames.Add(new CafeGame
                        {
                            CafeId = cafeId,
                            GameId = game.GameId,
                            IsAvailable = true,
                            LastVerified = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        cafeGame.LastVerified = DateTime.UtcNow;
                        cafeGame.IsAvailable = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving found game {Name} for cafe {Id}", gameData.Name, cafeId);
                }
            }
            if (added > 0) _logger.LogInformation("Added {Count} new games for cafe {Id}", added, cafeId);
        }
    }
}
