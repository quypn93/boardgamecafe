using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Services
{
    internal class InternalSearchResult
    {
        public List<VenueListItemDto> Venues { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class VenueService : IVenueService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VenueService> _logger;

        public VenueService(ApplicationDbContext context, ILogger<VenueService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VenueSearchResultDto>> SearchNearbyAsync(VenueSearchRequest request)
        {
            var result = await SearchVenuesInternalAsync(request);
            return result.Venues.Select(v => new VenueSearchResultDto
            {
                VenueId = v.VenueId,
                Name = v.Name,
                Slug = v.Slug,
                Address = v.Address,
                City = v.City,
                State = v.State,
                Country = v.Country,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                LocalImagePath = v.LocalImagePath,
                AverageRating = v.AverageRating,
                TotalReviews = v.TotalReviews,
                TotalRooms = v.TotalRooms,
                IsPremium = v.IsPremium,
                DistanceKm = v.DistanceKm
            }).ToList();
        }

        public async Task<VenueSearchPagedResult> SearchVenuesPagedAsync(VenueSearchRequest request)
        {
            var result = await SearchVenuesInternalAsync(request);
            return new VenueSearchPagedResult
            {
                Venues = result.Venues,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }

        public async Task<List<EscapeRoomVenue>> SearchByTextAsync(string query, int limit = 10)
        {
            var searchTerm = query.ToLower();
            return await _context.Venues
                .Include(v => v.Rooms)
                .Where(v => v.IsActive &&
                    (v.Name.ToLower().Contains(searchTerm) ||
                     v.City.ToLower().Contains(searchTerm)))
                .OrderByDescending(v => v.IsPremium)
                .ThenByDescending(v => v.AverageRating)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<VenueSearchResultDto>> FilterVenuesAsync(string? country, string? city, string? theme, int? difficulty, int? players, double? minRating)
        {
            var query = _context.Venues
                .Include(v => v.Rooms)
                .Where(v => v.IsActive)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(country))
                query = query.Where(v => v.Country.ToLower() == country.ToLower());

            if (!string.IsNullOrEmpty(city))
                query = query.Where(v => v.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(theme))
                query = query.Where(v => v.Rooms.Any(r => r.Theme.ToLower() == theme.ToLower() && r.IsActive));

            if (difficulty.HasValue)
                query = query.Where(v => v.Rooms.Any(r => r.Difficulty == difficulty.Value && r.IsActive));

            if (players.HasValue)
                query = query.Where(v => v.Rooms.Any(r => r.MinPlayers <= players.Value && r.MaxPlayers >= players.Value && r.IsActive));

            if (minRating.HasValue)
                query = query.Where(v => v.AverageRating >= (decimal)minRating.Value);

            var venues = await query
                .OrderByDescending(v => v.IsPremium)
                .ThenByDescending(v => v.AverageRating)
                .Take(100)
                .ToListAsync();

            return venues.Select(v => new VenueSearchResultDto
            {
                VenueId = v.VenueId,
                Name = v.Name,
                Slug = v.Slug,
                Address = v.Address,
                City = v.City,
                State = v.State,
                Country = v.Country,
                Latitude = v.Latitude,
                Longitude = v.Longitude,
                LocalImagePath = v.LocalImagePath,
                AverageRating = v.AverageRating,
                TotalReviews = v.TotalReviews,
                TotalRooms = v.TotalRooms,
                IsPremium = v.IsPremium
            }).ToList();
        }

        public async Task<EscapeRoomVenue?> GetByIdAsync(int venueId)
        {
            return await GetVenueByIdInternalAsync(venueId);
        }

        public async Task<EscapeRoomVenue?> GetBySlugAsync(string slug)
        {
            return await GetVenueBySlugInternalAsync(slug);
        }

        private async Task<InternalSearchResult> SearchVenuesInternalAsync(VenueSearchRequest request)
        {
            var query = _context.Venues
                .Include(v => v.Rooms)
                .Where(v => v.IsActive)
                .AsNoTracking();

            // Text search
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var searchTerm = request.Query.ToLower();
                query = query.Where(v =>
                    v.Name.ToLower().Contains(searchTerm) ||
                    v.City.ToLower().Contains(searchTerm) ||
                    (v.Description != null && v.Description.ToLower().Contains(searchTerm)));
            }

            // City filter
            if (!string.IsNullOrWhiteSpace(request.City))
            {
                query = query.Where(v => v.City.ToLower() == request.City.ToLower());
            }

            // Country filter
            if (!string.IsNullOrWhiteSpace(request.Country))
            {
                query = query.Where(v => v.Country.ToLower() == request.Country.ToLower());
            }

            // Theme filter
            if (!string.IsNullOrWhiteSpace(request.Theme))
            {
                query = query.Where(v => v.Rooms.Any(r => r.Theme.ToLower() == request.Theme.ToLower() && r.IsActive));
            }

            // Difficulty filters
            if (request.MinDifficulty.HasValue)
            {
                query = query.Where(v => v.Rooms.Any(r => r.Difficulty >= request.MinDifficulty.Value && r.IsActive));
            }
            if (request.MaxDifficulty.HasValue)
            {
                query = query.Where(v => v.Rooms.Any(r => r.Difficulty <= request.MaxDifficulty.Value && r.IsActive));
            }

            // Player count filter
            if (request.MinPlayers.HasValue)
            {
                query = query.Where(v => v.Rooms.Any(r => r.MaxPlayers >= request.MinPlayers.Value && r.IsActive));
            }
            if (request.MaxPlayers.HasValue)
            {
                query = query.Where(v => v.Rooms.Any(r => r.MinPlayers <= request.MaxPlayers.Value && r.IsActive));
            }

            // Kid-friendly filter
            if (request.IsKidFriendly == true)
            {
                query = query.Where(v => v.Rooms.Any(r => r.IsKidFriendly && r.IsActive));
            }

            // Wheelchair accessible filter
            if (request.IsWheelchairAccessible == true)
            {
                query = query.Where(v => v.IsWheelchairAccessible);
            }

            // Location-based search
            if (request.Latitude.HasValue && request.Longitude.HasValue && request.RadiusKm.HasValue)
            {
                var lat = request.Latitude.Value;
                var lng = request.Longitude.Value;
                var radius = request.RadiusKm.Value;

                // Bounding box filter first for performance
                var latDelta = radius / 111.0;
                var lngDelta = radius / (111.0 * Math.Cos(lat * Math.PI / 180));

                query = query.Where(v =>
                    v.Latitude >= lat - latDelta && v.Latitude <= lat + latDelta &&
                    v.Longitude >= lng - lngDelta && v.Longitude <= lng + lngDelta);
            }

            // Get total count
            var totalCount = await query.CountAsync();

            // Sorting
            query = request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending ? query.OrderByDescending(v => v.Name) : query.OrderBy(v => v.Name),
                "rating" => request.SortDescending ? query.OrderByDescending(v => v.AverageRating ?? 0) : query.OrderBy(v => v.AverageRating ?? 0),
                "rooms_count" => request.SortDescending ? query.OrderByDescending(v => v.TotalRooms) : query.OrderBy(v => v.TotalRooms),
                "reviews" => request.SortDescending ? query.OrderByDescending(v => v.TotalReviews) : query.OrderBy(v => v.TotalReviews),
                _ => query.OrderByDescending(v => v.IsPremium).ThenByDescending(v => v.AverageRating ?? 0)
            };

            // Pagination
            var skip = (request.Page - 1) * request.PageSize;
            var venues = await query
                .Skip(skip)
                .Take(request.PageSize)
                .ToListAsync();

            // Calculate distances if location provided
            var venueItems = venues.Select(v =>
            {
                var item = new VenueListItemDto
                {
                    VenueId = v.VenueId,
                    Name = v.Name,
                    Slug = v.Slug,
                    Address = v.Address,
                    City = v.City,
                    State = v.State,
                    Country = v.Country,
                    Latitude = v.Latitude,
                    Longitude = v.Longitude,
                    Phone = v.Phone,
                    Website = v.Website,
                    LocalImagePath = v.LocalImagePath,
                    AverageRating = v.AverageRating,
                    TotalReviews = v.TotalReviews,
                    TotalRooms = v.TotalRooms,
                    IsVerified = v.IsVerified,
                    IsPremium = v.IsPremium,
                    Rooms = v.Rooms.Where(r => r.IsActive).Select(r => new RoomSummaryDto
                    {
                        RoomId = r.RoomId,
                        Name = r.Name,
                        Slug = r.Slug,
                        Theme = r.Theme,
                        Difficulty = r.Difficulty,
                        MinPlayers = r.MinPlayers,
                        MaxPlayers = r.MaxPlayers,
                        DurationMinutes = r.DurationMinutes,
                        PricePerPerson = r.PricePerPerson,
                        SuccessRate = r.SuccessRate,
                        AverageRating = r.AverageRating,
                        TotalReviews = r.TotalReviews,
                        LocalImagePath = r.LocalImagePath
                    }).ToList()
                };

                // Set aggregated room data
                if (item.Rooms.Any())
                {
                    item.MostPopularTheme = item.Rooms
                        .GroupBy(r => r.Theme)
                        .OrderByDescending(g => g.Count())
                        .First().Key;
                    item.LowestDifficulty = item.Rooms.Min(r => r.Difficulty);
                    item.HighestDifficulty = item.Rooms.Max(r => r.Difficulty);
                }

                // Calculate distance
                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    item.DistanceKm = CalculateDistance(
                        request.Latitude.Value, request.Longitude.Value,
                        v.Latitude, v.Longitude);
                }

                return item;
            }).ToList();

            // Sort by distance if location search
            if (request.Latitude.HasValue && request.Longitude.HasValue && string.IsNullOrEmpty(request.SortBy))
            {
                venueItems = venueItems.OrderBy(v => v.DistanceKm).ToList();
            }

            return new InternalSearchResult
            {
                Venues = venueItems,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        private async Task<EscapeRoomVenue?> GetVenueByIdInternalAsync(int venueId)
        {
            return await _context.Venues
                .Include(v => v.Rooms.Where(r => r.IsActive))
                .Include(v => v.Reviews.Where(r => r.IsApproved))
                .Include(v => v.Photos.Where(p => p.IsApproved))
                .Include(v => v.PremiumListing)
                .FirstOrDefaultAsync(v => v.VenueId == venueId && v.IsActive);
        }

        private async Task<EscapeRoomVenue?> GetVenueBySlugInternalAsync(string slug)
        {
            return await _context.Venues
                .Include(v => v.Rooms.Where(r => r.IsActive))
                .Include(v => v.Reviews.Where(r => r.IsApproved))
                .Include(v => v.Photos.Where(p => p.IsApproved))
                .Include(v => v.PremiumListing)
                .FirstOrDefaultAsync(v => v.Slug == slug && v.IsActive);
        }

        public async Task<List<EscapeRoomVenue>> GetFeaturedVenuesAsync(int count = 10)
        {
            return await _context.Venues
                .Include(v => v.Rooms.Where(r => r.IsActive))
                .Where(v => v.IsActive && v.IsPremium)
                .OrderByDescending(v => v.AverageRating)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<EscapeRoomVenue>> GetNearbyVenuesAsync(double latitude, double longitude, double radiusKm = 50, int limit = 20)
        {
            var latDelta = radiusKm / 111.0;
            var lngDelta = radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180));

            var venues = await _context.Venues
                .Include(v => v.Rooms.Where(r => r.IsActive))
                .Where(v => v.IsActive &&
                    v.Latitude >= latitude - latDelta && v.Latitude <= latitude + latDelta &&
                    v.Longitude >= longitude - lngDelta && v.Longitude <= longitude + lngDelta)
                .AsNoTracking()
                .ToListAsync();

            return venues
                .Select(v =>
                {
                    v.DistanceKm = CalculateDistance(latitude, longitude, v.Latitude, v.Longitude);
                    return v;
                })
                .Where(v => v.DistanceKm <= radiusKm)
                .OrderBy(v => v.DistanceKm)
                .Take(limit)
                .ToList();
        }

        public async Task<EscapeRoomVenue> CreateVenueAsync(EscapeRoomVenue venue)
        {
            venue.Slug = GenerateSlug(venue.Name, venue.City);
            venue.CreatedAt = DateTime.UtcNow;
            venue.UpdatedAt = DateTime.UtcNow;

            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            return venue;
        }

        public async Task<EscapeRoomVenue> UpdateVenueAsync(EscapeRoomVenue venue)
        {
            venue.UpdatedAt = DateTime.UtcNow;
            _context.Venues.Update(venue);
            await _context.SaveChangesAsync();

            return venue;
        }

        public async Task<bool> DeleteVenueAsync(int venueId)
        {
            var venue = await _context.Venues.FindAsync(venueId);
            if (venue == null) return false;

            venue.IsActive = false;
            venue.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task UpdateVenueRatingAsync(int venueId)
        {
            var venue = await _context.Venues.FindAsync(venueId);
            if (venue == null) return;

            var reviews = await _context.Reviews
                .Where(r => r.VenueId == venueId && r.IsApproved)
                .ToListAsync();

            if (reviews.Any())
            {
                venue.AverageRating = (decimal)reviews.Average(r => r.Rating);
                venue.TotalReviews = reviews.Count;
            }
            else
            {
                venue.AverageRating = null;
                venue.TotalReviews = 0;
            }

            venue.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetAllCitiesAsync()
        {
            return await _context.Venues
                .Where(v => v.IsActive)
                .Select(v => v.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllThemesAsync()
        {
            return await _context.Rooms
                .Where(r => r.IsActive)
                .Select(r => r.Theme)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        public async Task<int> GetTotalVenueCountAsync()
        {
            return await _context.Venues.CountAsync(v => v.IsActive);
        }

        public async Task<int> GetTotalRoomCountAsync()
        {
            return await _context.Rooms.CountAsync(r => r.IsActive);
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in km
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180);

        private string GenerateSlug(string name, string city)
        {
            var slug = $"{name}-{city}".ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            // Ensure uniqueness
            var baseSlug = slug;
            var counter = 1;
            while (_context.Venues.Any(v => v.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }
    }
}
