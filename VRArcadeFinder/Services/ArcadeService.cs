using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace VRArcadeFinder.Services
{
    public interface IArcadeService
    {
        Task<List<ArcadeSearchResultDto>> SearchNearbyAsync(ArcadeSearchRequest request);
        Task<List<ArcadeSearchResultDto>> FilterArcadesAsync(string? country, string? city, bool openNow, bool hasGames, double? minRating, string? vrPlatform = null, List<string>? categories = null);
        Task<Arcade?> GetByIdAsync(int id);
        Task<Arcade?> GetBySlugAsync(string slug);
        Task<Arcade> CreateAsync(Arcade arcade);
        Task UpdateAsync(Arcade arcade);
        Task<List<Arcade>> GetByCityAsync(string city);
        Task<Arcade?> GetArcadeByIdAsync(int id);
        Task<Arcade?> AddArcadeAsync(Arcade arcade);
        Task AddReviewsAsync(int arcadeId, List<Review> reviews);
        Task<bool> ArcadeExistsAsync(string slug);
        Task<List<Arcade>> SearchByTextAsync(string query, int limit = 10);
    }

    public class ArcadeService : IArcadeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ArcadeService> _logger;

        public ArcadeService(ApplicationDbContext context, ILogger<ArcadeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ArcadeSearchResultDto>> SearchNearbyAsync(ArcadeSearchRequest request)
        {
            try
            {
                // Calculate bounding box for initial filter (performance optimization)
                var radiusKm = request.Radius / 1000.0;
                var latDelta = radiusKm / 111.0; // 1 degree latitude = 111 km
                var lonDelta = radiusKm / (111.0 * Math.Cos(request.Latitude * Math.PI / 180.0));

                // Query database with bounding box filter - OPTIMIZED: only select needed fields
                var query = _context.Arcades
                    .AsNoTracking() // Performance: no change tracking needed
                    .Where(c => c.IsActive)
                    .Where(c => c.Latitude >= request.Latitude - latDelta && c.Latitude <= request.Latitude + latDelta)
                    .Where(c => c.Longitude >= request.Longitude - lonDelta && c.Longitude <= request.Longitude + lonDelta);

                // Apply filters
                if (request.HasGames)
                {
                    query = query.Where(c => c.ArcadeGames.Any());
                }

                if (request.MinRating.HasValue)
                {
                    query = query.Where(c => c.AverageRating >= request.MinRating.Value);
                }

                if (!string.IsNullOrEmpty(request.VRPlatform))
                {
                    query = query.Where(c => c.VRPlatforms != null && c.VRPlatforms.Contains(request.VRPlatform));
                }

                // Project to DTO directly in SQL - much faster than loading full entities
                var arcadeDtos = await query
                    .Select(c => new
                    {
                        c.ArcadeId,
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
                        c.VRPlatforms,
                        c.TotalVRStations,
                        c.HasMultiplayerArea,
                        TotalGames = c.ArcadeGames.Count,
                        ThumbnailUrl = c.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ThumbnailUrl).FirstOrDefault()
                    })
                    .ToListAsync();

                // Calculate exact distance using Haversine formula and filter by radius
                var results = arcadeDtos
                    .Select(c => new
                    {
                        Arcade = c,
                        Distance = CalculateDistance(request.Latitude, request.Longitude, c.Latitude, c.Longitude)
                    })
                    .Where(x => x.Distance <= request.Radius)
                    .OrderBy(x => x.Distance)
                    .Take(request.Limit)
                    .Select(x => new ArcadeSearchResultDto
                    {
                        Id = x.Arcade.ArcadeId,
                        Name = x.Arcade.Name,
                        Address = x.Arcade.Address,
                        City = x.Arcade.City,
                        State = x.Arcade.State,
                        Latitude = x.Arcade.Latitude,
                        Longitude = x.Arcade.Longitude,
                        Distance = x.Distance,
                        AverageRating = x.Arcade.AverageRating,
                        TotalReviews = x.Arcade.ApprovedReviewCount,
                        IsOpenNow = IsOpenNow(x.Arcade.OpeningHours),
                        IsPremium = x.Arcade.IsPremium,
                        IsVerified = x.Arcade.IsVerified,
                        TotalGames = x.Arcade.TotalGames,
                        Phone = x.Arcade.Phone,
                        Website = x.Arcade.Website,
                        Slug = x.Arcade.Slug,
                        ThumbnailUrl = x.Arcade.ThumbnailUrl,
                        PriceRange = x.Arcade.PriceRange,
                        VRPlatforms = x.Arcade.VRPlatforms,
                        TotalVRStations = x.Arcade.TotalVRStations,
                        HasMultiplayerArea = x.Arcade.HasMultiplayerArea
                    })
                    .ToList();

                // Filter by open now if requested
                if (request.OpenNow)
                {
                    results = results.Where(r => r.IsOpenNow).ToList();
                }

                _logger.LogInformation($"Found {results.Count} arcades within {request.Radius}m of ({request.Latitude}, {request.Longitude})");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for arcades");
                throw;
            }
        }

        // Helper method to check if arcade is open now based on opening hours string
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

        public async Task<Arcade?> GetByIdAsync(int id)
        {
            return await _context.Arcades
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Photos)
                .Include(c => c.ArcadeGames)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Events)
                .Include(c => c.PremiumListing)
                .FirstOrDefaultAsync(c => c.ArcadeId == id && c.IsActive);
        }

        public async Task<Arcade?> GetBySlugAsync(string slug)
        {
            return await _context.Arcades
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Photos)
                .Include(c => c.ArcadeGames)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Events)
                .Include(c => c.PremiumListing)
                .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);
        }

        public async Task<Arcade> CreateAsync(Arcade arcade)
        {
            arcade.CreatedAt = DateTime.UtcNow;
            arcade.UpdatedAt = DateTime.UtcNow;

            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new arcade: {arcade.Name} (ID: {arcade.ArcadeId})");

            return arcade;
        }

        public async Task UpdateAsync(Arcade arcade)
        {
            arcade.UpdatedAt = DateTime.UtcNow;

            _context.Arcades.Update(arcade);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated arcade: {arcade.Name} (ID: {arcade.ArcadeId})");
        }

        public async Task<List<Arcade>> GetByCityAsync(string city)
        {
            return await _context.Arcades
                .Where(c => c.City == city && c.IsActive)
                .Include(c => c.ArcadeGames)
                .Include(c => c.Photos)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Arcade?> GetArcadeByIdAsync(int id)
        {
            return await _context.Arcades.FirstOrDefaultAsync(c => c.ArcadeId == id);
        }

        public async Task<Arcade?> AddArcadeAsync(Arcade arcade)
        {
            try
            {
                // Check if arcade with same slug already exists
                var existingArcade = await _context.Arcades.FirstOrDefaultAsync(c => c.Slug == arcade.Slug);
                if (existingArcade != null)
                {
                    _logger.LogWarning("Arcade with slug {Slug} already exists", arcade.Slug);
                    return existingArcade;
                }

                _context.Arcades.Add(arcade);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added arcade: {Name} with ID: {ArcadeId}", arcade.Name, arcade.ArcadeId);
                return arcade;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding arcade: {Name}", arcade.Name);
                return null;
            }
        }

        public async Task AddReviewsAsync(int arcadeId, List<Review> reviews)
        {
            try
            {
                if (!reviews.Any())
                {
                    _logger.LogInformation("No reviews to add for arcade {ArcadeId}", arcadeId);
                    return;
                }

                // Check for existing reviews from same sources to avoid duplicates
                var existingReviewTexts = await _context.Reviews
                    .Where(r => r.ArcadeId == arcadeId)
                    .Select(r => r.Content)
                    .ToListAsync();

                var newReviews = reviews
                    .Where(r => !existingReviewTexts.Contains(r.Content))
                    .ToList();

                if (newReviews.Any())
                {
                    _context.Reviews.AddRange(newReviews);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Added {Count} reviews for arcade {ArcadeId}", newReviews.Count, arcadeId);
                }
                else
                {
                    _logger.LogInformation("All reviews already exist for arcade {ArcadeId}", arcadeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding reviews for arcade {ArcadeId}", arcadeId);
            }
        }

        public async Task<bool> ArcadeExistsAsync(string slug)
        {
            return await _context.Arcades.AnyAsync(c => c.Slug == slug);
        }

        public async Task<List<Arcade>> SearchByTextAsync(string query, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Arcade>();

            var searchTerm = query.ToLower().Trim();

            return await _context.Arcades
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Where(c => c.Name.ToLower().Contains(searchTerm) ||
                           c.City.ToLower().Contains(searchTerm) ||
                           (c.Address != null && c.Address.ToLower().Contains(searchTerm)))
                .OrderBy(c => c.Name)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<ArcadeSearchResultDto>> FilterArcadesAsync(string? country, string? city, bool openNow, bool hasGames, double? minRating, string? vrPlatform = null, List<string>? categories = null)
        {
            try
            {
                var query = _context.Arcades
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
                    query = query.Where(c => c.ArcadeGames.Any());
                }

                // Filter by VR platform
                if (!string.IsNullOrEmpty(vrPlatform))
                {
                    query = query.Where(c => c.VRPlatforms != null && c.VRPlatforms.Contains(vrPlatform));
                }

                // Filter by game categories (multiselect - any match)
                if (categories != null && categories.Any())
                {
                    query = query.Where(c => c.ArcadeGames.Any(cg => cg.Game != null && categories.Contains(cg.Game.Category)));
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
                    .Select(c => new ArcadeSearchResultDto
                    {
                        Id = c.ArcadeId,
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
                        TotalGames = c.ArcadeGames.Count,
                        Phone = c.Phone,
                        Website = c.Website,
                        Slug = c.Slug,
                        ThumbnailUrl = c.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ThumbnailUrl).FirstOrDefault(),
                        PriceRange = c.PriceRange,
                        VRPlatforms = c.VRPlatforms,
                        TotalVRStations = c.TotalVRStations,
                        HasMultiplayerArea = c.HasMultiplayerArea,
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

                _logger.LogInformation($"Found {results.Count} arcades for filter: Country={country}, City={city}");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering arcades");
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

        #endregion
    }
}
