using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using BoardGameCafeFinder.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Services
{
    public interface ICafeService
    {
        Task<List<CafeSearchResultDto>> SearchNearbyAsync(CafeSearchRequest request);
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

                // Query database with bounding box filter
                var query = _context.Cafes
                    .Where(c => c.IsActive)
                    .Where(c => c.Latitude >= request.Latitude - latDelta && c.Latitude <= request.Latitude + latDelta)
                    .Where(c => c.Longitude >= request.Longitude - lonDelta && c.Longitude <= request.Longitude + lonDelta)
                    .Include(c => c.CafeGames)
                    .Include(c => c.CafeGames)
                    .Include(c => c.Photos)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.User)
                    .AsQueryable();

                // Apply filters
                if (request.HasGames)
                {
                    query = query.Where(c => c.CafeGames.Any());
                }

                if (request.MinRating.HasValue)
                {
                    query = query.Where(c => c.AverageRating >= request.MinRating.Value);
                }

                var cafes = await query.ToListAsync();

                // Calculate exact distance using Haversine formula and filter by radius
                var results = cafes
                    .Select(c => new
                    {
                        Cafe = c,
                        Distance = CalculateDistance(request.Latitude, request.Longitude, c.Latitude, c.Longitude)
                    })
                    .Where(x => x.Distance <= request.Radius)
                    .OrderBy(x => x.Distance)
                    .Take(request.Limit)
                    .Select(x => MapToCafeSearchResultDto(x.Cafe, x.Distance))
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
                TotalReviews = cafe.TotalReviews,
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
