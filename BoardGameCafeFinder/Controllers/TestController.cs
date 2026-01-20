using BoardGameCafeFinder.Models.DTOs;
using BoardGameCafeFinder.Services;
using BoardGameCafeFinder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers
{
    public class CrawlRequest
    {
        public string Location { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 20;
    }
    /// <summary>
    /// Test controller for testing features without Google Maps API key
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class TestController : Controller
    {
        private readonly ICafeService _cafeService;
        private readonly IGoogleMapsCrawlerService _crawlerService;
        private readonly IBggSyncService _bggSyncService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestController> _logger;
        private readonly IWebHostEnvironment _environment;

        public TestController(
            ICafeService cafeService,
            IGoogleMapsCrawlerService crawlerService,
            IBggSyncService bggSyncService,
            ApplicationDbContext context,
            ILogger<TestController> logger,
            IWebHostEnvironment environment)
        {
            _cafeService = cafeService;
            _crawlerService = crawlerService;
            _bggSyncService = bggSyncService;
            _context = context;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Test page to see all cafés without map
        /// GET: /Test/Cafes
        /// </summary>
        public async Task<IActionResult> Cafes()
        {
            // Get sample search results (Seattle coordinates)
            var request = new CafeSearchRequest
            {
                Latitude = 47.6062,
                Longitude = -122.3321,
                Radius = 50000, // 50km - show all sample data
                OpenNow = false,
                HasGames = false,
                Limit = 50
            };

            var cafes = await _cafeService.SearchNearbyAsync(request);

            return View(cafes);
        }

        /// <summary>
        /// Test API endpoints directly
        /// GET: /Test/Api
        /// </summary>
        public IActionResult Api()
        {
            return View();
        }

        /// <summary>
        /// Test geospatial distance calculations
        /// GET: /Test/Distance
        /// </summary>
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
        [HttpPost]
        public async Task<IActionResult> CrawlAndSave([FromBody] CrawlRequest request)
        {
            _logger.LogInformation("Starting CrawlAndSave for location: {Location}", request.Location);

            var results = await _crawlerService.CrawlBoardGameCafesAsync(request.Location, request.MaxResults);

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
        public async Task<IActionResult> SyncBgg(int cafeId)
        {
            var result = await _bggSyncService.SyncCafeGamesAsync(cafeId);
            return Json(result);
        }

        /// <summary>
        /// Link a BGG username to a cafe
        /// GET: /Test/LinkBgg?cafeId=1&bggUsername=moxboardinghouse
        /// </summary>
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
        /// View with buttons to crawl major cities
        /// GET: /Test/CrawlManager
        /// </summary>
        public IActionResult CrawlManager()
        {
            return View();
        }

        /// <summary>
        /// Clean all phone numbers in database by removing special/control characters
        /// POST: /Test/CleanPhoneNumbers
        /// </summary>
        [HttpPost]
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
        [HttpPost]
        public async Task<IActionResult> ClearAllData()
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

                _logger.LogInformation("Cleared all data: {CafeCount} cafes, {ReviewCount} reviews, {PhotoCount} photos, {EventCount} events, {CafeGameCount} cafe-games, {PremiumListingCount} premium listings, {ImagesDeleted} image files",
                    cafeCount, reviewCount, photoCount, eventCount, cafeGameCount, premiumListingCount, imagesDeleted);

                return Json(new
                {
                    Success = true,
                    Message = "All data cleared successfully",
                    Deleted = new
                    {
                        Cafes = cafeCount,
                        Reviews = reviewCount,
                        Photos = photoCount,
                        Events = eventCount,
                        CafeGames = cafeGameCount,
                        PremiumListings = premiumListingCount,
                        ImageFiles = imagesDeleted
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all data");
                return Json(new
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
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
                    Url = photoUrls[i],
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
    }
}
