using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using BoardGameCafeFinder.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Services
{
    public interface ICafeService
    {
        Task<List<CafeSearchResultDto>> SearchNearbyAsync(CafeSearchRequest request);
        Task<List<CafeSearchResultDto>> FilterCafesAsync(string? country, string? city, bool openNow, bool hasGames, double? minRating, List<string>? categories = null);
        Task<Cafe?> GetByIdAsync(int id);
        Task<Cafe?> GetBySlugAsync(string slug);
        Task<Cafe> CreateAsync(Cafe cafe);
        Task UpdateAsync(Cafe cafe);
        Task<List<Cafe>> GetByCityAsync(string city);
        Task<Cafe?> GetCafeByIdAsync(int id);
        Task<Cafe?> AddCafeAsync(Cafe cafe);
        Task AddReviewsAsync(int cafeId, List<Review> reviews);
        Task<bool> CafeExistsAsync(string slug);
    }

    public class CafeService : ICafeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CafeService> _logger;

        public CafeService(ApplicationDbContext context, ILogger<CafeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CafeSearchResultDto>> SearchNearbyAsync(CafeSearchRequest request)
        {
            try
            {
                // Calculate bounding box for initial filter (performance optimization)
                var radiusKm = request.Radius / 1000.0;
                var latDelta = radiusKm / 111.0; // 1 degree latitude ≈ 111 km
                var lonDelta = radiusKm / (111.0 * Math.Cos(request.Latitude * Math.PI / 180.0));

                // Query database with bounding box filter - OPTIMIZED: only select needed fields
                var query = _context.Cafes
                    .AsNoTracking() // Performance: no change tracking needed
                    .Where(c => c.IsActive)
                    .Where(c => c.Latitude >= request.Latitude - latDelta && c.Latitude <= request.Latitude + latDelta)
                    .Where(c => c.Longitude >= request.Longitude - lonDelta && c.Longitude <= request.Longitude + lonDelta);

                // Apply filters
                if (request.HasGames)
                {
                    query = query.Where(c => c.CafeGames.Any());
                }

                if (request.MinRating.HasValue)
                {
                    query = query.Where(c => c.AverageRating >= request.MinRating.Value);
                }

                // Project to DTO directly in SQL - much faster than loading full entities
                var cafeDtos = await query
                    .Select(c => new
                    {
                        c.CafeId,
                        c.Name,
                        c.Address,
                        c.City,
                        c.State,
                        c.Latitude,
                        c.Longitude,
                        c.AverageRating,
                        ApprovedReviewCount = c.Reviews.Count(r => r.UserId == null || r.IsApproved),
                        c.IsPremium,
                        c.IsVerified,
                        c.Phone,
                        c.Website,
                        c.Slug,
                        c.PriceRange,
                        c.OpeningHours,
                        TotalGames = c.CafeGames.Count,
                        ThumbnailUrl = c.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ThumbnailUrl).FirstOrDefault()
                    })
                    .ToListAsync();

                // Calculate exact distance using Haversine formula and filter by radius
                var results = cafeDtos
                    .Select(c => new
                    {
                        Cafe = c,
                        Distance = CalculateDistance(request.Latitude, request.Longitude, c.Latitude, c.Longitude)
                    })
                    .Where(x => x.Distance <= request.Radius)
                    .OrderBy(x => x.Distance)
                    .Take(request.Limit)
                    .Select(x => new CafeSearchResultDto
                    {
                        Id = x.Cafe.CafeId,
                        Name = x.Cafe.Name,
                        Address = x.Cafe.Address,
                        City = x.Cafe.City,
                        State = x.Cafe.State,
                        Latitude = x.Cafe.Latitude,
                        Longitude = x.Cafe.Longitude,
                        Distance = x.Distance,
                        AverageRating = x.Cafe.AverageRating,
                        TotalReviews = x.Cafe.ApprovedReviewCount,
                        IsOpenNow = IsOpenNow(x.Cafe.OpeningHours),
                        IsPremium = x.Cafe.IsPremium,
                        IsVerified = x.Cafe.IsVerified,
                        TotalGames = x.Cafe.TotalGames,
                        Phone = x.Cafe.Phone,
                        Website = x.Cafe.Website,
                        Slug = x.Cafe.Slug,
                        ThumbnailUrl = x.Cafe.ThumbnailUrl,
                        PriceRange = x.Cafe.PriceRange
                    })
                    .ToList();

                // Filter by open now if requested
                if (request.OpenNow)
                {
                    results = results.Where(r => r.IsOpenNow).ToList();
                }

                _logger.LogInformation($"Found {results.Count} cafés within {request.Radius}m of ({request.Latitude}, {request.Longitude})");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for cafés");
                throw;
            }
        }

        // Helper method to check if cafe is open now based on opening hours string
        private bool IsOpenNow(string? openingHours)
        {
            if (string.IsNullOrEmpty(openingHours)) return false;

            try
            {
                var now = DateTime.Now;
                var dayOfWeek = now.DayOfWeek.ToString().ToLower();
                var currentTime = now.TimeOfDay;

                // Parse opening hours (format: "Mon: 9:00-22:00, Tue: 9:00-22:00, ...")
                var days = openingHours.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var day in days)
                {
                    var parts = day.Split(':', 2);
                    if (parts.Length < 2) continue;

                    var dayName = parts[0].Trim().ToLower();
                    if (!dayName.StartsWith(dayOfWeek.Substring(0, 3))) continue;

                    var timeParts = parts[1].Trim().Split('-');
                    if (timeParts.Length < 2) continue;

                    if (TimeSpan.TryParse(timeParts[0].Trim(), out var openTime) &&
                        TimeSpan.TryParse(timeParts[1].Trim(), out var closeTime))
                    {
                        return currentTime >= openTime && currentTime <= closeTime;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return false;
        }

        public async Task<Cafe?> GetByIdAsync(int id)
        {
            return await _context.Cafes
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Photos)
                .Include(c => c.CafeGames)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Events)
                .Include(c => c.PremiumListing)
                .FirstOrDefaultAsync(c => c.CafeId == id && c.IsActive);
        }

        public async Task<Cafe?> GetBySlugAsync(string slug)
        {
            return await _context.Cafes
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Photos)
                .Include(c => c.CafeGames)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Events)
                .Include(c => c.PremiumListing)
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);
        }

        public async Task<Cafe> CreateAsync(Cafe cafe)
        {
            cafe.CreatedAt = DateTime.UtcNow;
            cafe.UpdatedAt = DateTime.UtcNow;

            _context.Cafes.Add(cafe);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new café: {cafe.Name} (ID: {cafe.CafeId})");

            return cafe;
        }

        public async Task UpdateAsync(Cafe cafe)
        {
            cafe.UpdatedAt = DateTime.UtcNow;

            _context.Cafes.Update(cafe);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated café: {cafe.Name} (ID: {cafe.CafeId})");
        }

        public async Task<List<Cafe>> GetByCityAsync(string city)
        {
            return await _context.Cafes
                .Where(c => c.City == city && c.IsActive)
                .Include(c => c.CafeGames)
                .Include(c => c.Photos)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Cafe?> GetCafeByIdAsync(int id)
        {
            return await _context.Cafes.FirstOrDefaultAsync(c => c.CafeId == id);
        }

        public async Task<Cafe?> AddCafeAsync(Cafe cafe)
        {
            try
            {
                // Check if cafe with same slug already exists
                var existingCafe = await _context.Cafes.FirstOrDefaultAsync(c => c.Slug == cafe.Slug);
                if (existingCafe != null)
                {
                    _logger.LogWarning("Cafe with slug {Slug} already exists", cafe.Slug);
                    return existingCafe;
                }

                _context.Cafes.Add(cafe);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added cafe: {Name} with ID: {CafeId}", cafe.Name, cafe.CafeId);
                return cafe;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding cafe: {Name}", cafe.Name);
                return null;
            }
        }

        public async Task AddReviewsAsync(int cafeId, List<Review> reviews)
        {
            try
            {
                if (!reviews.Any())
                {
                    _logger.LogInformation("No reviews to add for cafe {CafeId}", cafeId);
                    return;
                }

                // Check for existing reviews from same sources to avoid duplicates
                var existingReviewTexts = await _context.Reviews
                    .Where(r => r.CafeId == cafeId)
                    .Select(r => r.Content)
                    .ToListAsync();

                var newReviews = reviews
                    .Where(r => !existingReviewTexts.Contains(r.Content))
                    .ToList();

                if (newReviews.Any())
                {
                    _context.Reviews.AddRange(newReviews);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Added {Count} reviews for cafe {CafeId}", newReviews.Count, cafeId);
                }
                else
                {
                    _logger.LogInformation("All reviews already exist for cafe {CafeId}", cafeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reviews for cafe {CafeId}", cafeId);
            }
        }

        public async Task<bool> CafeExistsAsync(string slug)
        {
            return await _context.Cafes.AnyAsync(c => c.Slug == slug);
        }

        public async Task<List<CafeSearchResultDto>> FilterCafesAsync(string? country, string? city, bool openNow, bool hasGames, double? minRating, List<string>? categories = null)
        {
            try
            {
                var query = _context.Cafes
                    .AsNoTracking() // Performance: no change tracking
                    .Where(c => c.IsActive);

                // Filter by country
                if (!string.IsNullOrEmpty(country))
                {
                    query = query.Where(c => c.Country == country);
                }

                // Filter by city
                if (!string.IsNullOrEmpty(city))
                {
                    query = query.Where(c => c.City == city);
                }

                // Filter by has games
                if (hasGames)
                {
                    query = query.Where(c => c.CafeGames.Any());
                }

                // Filter by game categories (multiselect - any match)
                if (categories != null && categories.Any())
                {
                    query = query.Where(c => c.CafeGames.Any(cg => cg.Game != null && categories.Contains(cg.Game.Category)));
                }

                // Filter by minimum rating
                if (minRating.HasValue)
                {
                    var minRatingDecimal = (decimal)minRating.Value;
                    query = query.Where(c => c.AverageRating >= minRatingDecimal);
                }

                // Project to DTO directly in SQL - much faster than loading full entities
                var results = await query
                    .OrderByDescending(c => c.AverageRating)
                    .Take(100)
                    .Select(c => new CafeSearchResultDto
                    {
                        Id = c.CafeId,
                        Name = c.Name,
                        Address = c.Address,
                        City = c.City,
                        State = c.State,
                        Latitude = c.Latitude,
                        Longitude = c.Longitude,
                        Distance = 0,
                        AverageRating = c.AverageRating,
                        TotalReviews = c.Reviews.Count(r => r.UserId == null || r.IsApproved),
                        IsOpenNow = false, // Will be calculated after
                        IsPremium = c.IsPremium,
                        IsVerified = c.IsVerified,
                        TotalGames = c.CafeGames.Count,
                        Phone = c.Phone,
                        Website = c.Website,
                        Slug = c.Slug,
                        ThumbnailUrl = c.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ThumbnailUrl).FirstOrDefault(),
                        PriceRange = c.PriceRange,
                        OpeningHoursRaw = c.OpeningHours // Temp field for IsOpenNow calculation
                    })
                    .ToListAsync();

                // Calculate IsOpenNow in memory
                foreach (var result in results)
                {
                    result.IsOpenNow = IsOpenNow(result.OpeningHoursRaw);
                }

                // Filter by open now if requested (must be done in memory)
                if (openNow)
                {
                    results = results.Where(r => r.IsOpenNow).ToList();
                }

                _logger.LogInformation($"Found {results.Count} cafés for filter: Country={country}, City={city}");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering cafés");
                throw;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Calculate distance between two points using Haversine formula
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth's radius in meters

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // Distance in meters
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Map Cafe entity to CafeSearchResultDto
        /// </summary>
        private CafeSearchResultDto MapToCafeSearchResultDto(Cafe cafe, double distance)
        {
            return new CafeSearchResultDto
            {
                Id = cafe.CafeId,
                Name = cafe.Name,
                Address = cafe.Address,
                City = cafe.City,
                State = cafe.State,
                Latitude = cafe.Latitude,
                Longitude = cafe.Longitude,
                Distance = distance,
                AverageRating = cafe.AverageRating,
                TotalReviews = cafe.Reviews?.Count(r => r.UserId == null || r.IsApproved) ?? cafe.TotalReviews,
                IsOpenNow = cafe.IsOpenNow(),
                IsPremium = cafe.IsPremium,
                IsVerified = cafe.IsVerified,
                TotalGames = cafe.CafeGames?.Count ?? 0,
                Phone = cafe.Phone,
                Website = cafe.Website,
                Slug = cafe.Slug,
                ThumbnailUrl = cafe.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.ThumbnailUrl,
                PriceRange = cafe.PriceRange,
                LatestReviews = cafe.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(3)
                    .Select(r => new ReviewSummaryDto
                    {
                        Id = r.ReviewId,
                        AuthorName = r.User != null ? r.User.GetFullName() : r.Title?.Replace("Google Maps Review - ", "") ?? "Anonymous",
                        AuthorAvatarUrl = r.User?.AvatarUrl ?? "/images/default-avatar.png",
                        Rating = r.Rating,
                        Title = r.Title,
                        Content = r.Content?.Length > 100 ? r.Content.Substring(0, 97) + "..." : r.Content,
                        RelativeDate = r.GetTimeAgo(),
                        CreatedAt = r.CreatedAt
                    })
                    .ToList()
            };
        }

        #endregion
    }
}
