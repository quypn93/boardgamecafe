using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CookingClassFinder.Services
{
    internal class InternalSearchResult
    {
        public List<SchoolListItemDto> Schools { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class SchoolService : ISchoolService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SchoolService> _logger;

        public SchoolService(ApplicationDbContext context, ILogger<SchoolService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<SchoolSearchResultDto>> SearchNearbyAsync(SchoolSearchRequest request)
        {
            var result = await SearchSchoolsInternalAsync(request);
            return result.Schools.Select(s => new SchoolSearchResultDto
            {
                SchoolId = s.SchoolId,
                Name = s.Name,
                Slug = s.Slug,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Country = s.Country,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                LocalImagePath = s.LocalImagePath,
                AverageRating = s.AverageRating,
                TotalReviews = s.TotalReviews,
                TotalClasses = s.TotalClasses,
                IsPremium = s.IsPremium,
                DistanceKm = s.DistanceKm
            }).ToList();
        }

        public async Task<SchoolSearchPagedResult> SearchSchoolsPagedAsync(SchoolSearchRequest request)
        {
            var result = await SearchSchoolsInternalAsync(request);
            return new SchoolSearchPagedResult
            {
                Schools = result.Schools,
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }

        public async Task<List<CookingSchool>> SearchByTextAsync(string query, int limit = 10)
        {
            var searchTerm = query.ToLower();
            return await _context.Schools
                .Include(s => s.Classes)
                .Where(s => s.IsActive &&
                    (s.Name.ToLower().Contains(searchTerm) ||
                     s.City.ToLower().Contains(searchTerm)))
                .OrderByDescending(s => s.IsPremium)
                .ThenByDescending(s => s.AverageRating)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<SchoolSearchResultDto>> FilterSchoolsAsync(string? country, string? city, string? cuisineType, string? difficultyLevel, decimal? maxPrice, double? minRating)
        {
            var query = _context.Schools
                .Include(s => s.Classes)
                .Where(s => s.IsActive)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(country))
                query = query.Where(s => s.Country.ToLower() == country.ToLower());

            if (!string.IsNullOrEmpty(city))
                query = query.Where(s => s.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(cuisineType))
                query = query.Where(s => s.Classes.Any(c => c.CuisineType.ToLower() == cuisineType.ToLower() && c.IsActive));

            if (!string.IsNullOrEmpty(difficultyLevel))
                query = query.Where(s => s.Classes.Any(c => c.DifficultyLevel == difficultyLevel && c.IsActive));

            if (maxPrice.HasValue)
                query = query.Where(s => s.Classes.Any(c => c.PricePerPerson <= maxPrice.Value && c.IsActive));

            if (minRating.HasValue)
                query = query.Where(s => s.AverageRating >= (decimal)minRating.Value);

            var schools = await query
                .OrderByDescending(s => s.IsPremium)
                .ThenByDescending(s => s.AverageRating)
                .Take(100)
                .ToListAsync();

            return schools.Select(s => new SchoolSearchResultDto
            {
                SchoolId = s.SchoolId,
                Name = s.Name,
                Slug = s.Slug,
                Address = s.Address,
                City = s.City,
                State = s.State,
                Country = s.Country,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                LocalImagePath = s.LocalImagePath,
                AverageRating = s.AverageRating,
                TotalReviews = s.TotalReviews,
                TotalClasses = s.TotalClasses,
                IsPremium = s.IsPremium
            }).ToList();
        }

        public async Task<CookingSchool?> GetByIdAsync(int schoolId)
        {
            return await _context.Schools
                .Include(s => s.Classes.Where(c => c.IsActive))
                .Include(s => s.Reviews.Where(r => r.IsApproved))
                .Include(s => s.Photos.Where(p => p.IsApproved))
                .Include(s => s.PremiumListing)
                .FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.IsActive);
        }

        public async Task<CookingSchool?> GetBySlugAsync(string slug)
        {
            return await _context.Schools
                .Include(s => s.Classes.Where(c => c.IsActive))
                .Include(s => s.Reviews.Where(r => r.IsApproved))
                .Include(s => s.Photos.Where(p => p.IsApproved))
                .Include(s => s.PremiumListing)
                .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
        }

        private async Task<InternalSearchResult> SearchSchoolsInternalAsync(SchoolSearchRequest request)
        {
            var query = _context.Schools
                .Include(s => s.Classes)
                .Where(s => s.IsActive)
                .AsNoTracking();

            // Text search
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                var searchTerm = request.Query.ToLower();
                query = query.Where(s =>
                    s.Name.ToLower().Contains(searchTerm) ||
                    s.City.ToLower().Contains(searchTerm) ||
                    (s.Description != null && s.Description.ToLower().Contains(searchTerm)));
            }

            // City filter
            if (!string.IsNullOrWhiteSpace(request.City))
                query = query.Where(s => s.City.ToLower() == request.City.ToLower());

            // Country filter
            if (!string.IsNullOrWhiteSpace(request.Country))
                query = query.Where(s => s.Country.ToLower() == request.Country.ToLower());

            // Cuisine filter
            if (!string.IsNullOrWhiteSpace(request.CuisineType))
                query = query.Where(s => s.Classes.Any(c => c.CuisineType.ToLower() == request.CuisineType.ToLower() && c.IsActive));

            // Difficulty filter
            if (!string.IsNullOrWhiteSpace(request.DifficultyLevel))
                query = query.Where(s => s.Classes.Any(c => c.DifficultyLevel == request.DifficultyLevel && c.IsActive));

            // Price filter
            if (request.MaxPrice.HasValue)
                query = query.Where(s => s.Classes.Any(c => c.PricePerPerson <= request.MaxPrice.Value && c.IsActive));

            // Dietary filters
            if (request.IsVegetarian == true)
                query = query.Where(s => s.Classes.Any(c => c.IsVegetarian && c.IsActive));
            if (request.IsVegan == true)
                query = query.Where(s => s.Classes.Any(c => c.IsVegan && c.IsActive));

            // Class type filters
            if (request.IsKidsFriendly == true)
                query = query.Where(s => s.Classes.Any(c => c.IsKidsFriendly && c.IsActive));
            if (request.IsCouplesClass == true)
                query = query.Where(s => s.Classes.Any(c => c.IsCouplesClass && c.IsActive));
            if (request.IsOnline == true)
                query = query.Where(s => s.Classes.Any(c => c.IsOnline && c.IsActive));

            // Location-based search
            if (request.Latitude.HasValue && request.Longitude.HasValue && request.RadiusKm.HasValue)
            {
                var lat = request.Latitude.Value;
                var lng = request.Longitude.Value;
                var radius = request.RadiusKm.Value;

                var latDelta = radius / 111.0;
                var lngDelta = radius / (111.0 * Math.Cos(lat * Math.PI / 180));

                query = query.Where(s =>
                    s.Latitude >= lat - latDelta && s.Latitude <= lat + latDelta &&
                    s.Longitude >= lng - lngDelta && s.Longitude <= lng + lngDelta);
            }

            var totalCount = await query.CountAsync();

            // Sorting
            query = request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
                "rating" => request.SortDescending ? query.OrderByDescending(s => s.AverageRating ?? 0) : query.OrderBy(s => s.AverageRating ?? 0),
                "classes_count" => request.SortDescending ? query.OrderByDescending(s => s.TotalClasses) : query.OrderBy(s => s.TotalClasses),
                "reviews" => request.SortDescending ? query.OrderByDescending(s => s.TotalReviews) : query.OrderBy(s => s.TotalReviews),
                _ => query.OrderByDescending(s => s.IsPremium).ThenByDescending(s => s.AverageRating ?? 0)
            };

            var skip = (request.Page - 1) * request.PageSize;
            var schools = await query.Skip(skip).Take(request.PageSize).ToListAsync();

            var schoolItems = schools.Select(s =>
            {
                var item = new SchoolListItemDto
                {
                    SchoolId = s.SchoolId,
                    Name = s.Name,
                    Slug = s.Slug,
                    Address = s.Address,
                    City = s.City,
                    State = s.State,
                    Country = s.Country,
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Phone = s.Phone,
                    Website = s.Website,
                    LocalImagePath = s.LocalImagePath,
                    AverageRating = s.AverageRating,
                    TotalReviews = s.TotalReviews,
                    TotalClasses = s.TotalClasses,
                    IsVerified = s.IsVerified,
                    IsPremium = s.IsPremium,
                    CuisineSpecialties = s.GetCuisineSpecialtiesList(),
                    Classes = s.Classes.Where(c => c.IsActive).Select(c => new ClassSummaryDto
                    {
                        ClassId = c.ClassId,
                        Name = c.Name,
                        Slug = c.Slug,
                        CuisineType = c.CuisineType,
                        DifficultyLevel = c.DifficultyLevel,
                        MinStudents = c.MinStudents,
                        MaxStudents = c.MaxStudents,
                        DurationMinutes = c.DurationMinutes,
                        PricePerPerson = c.PricePerPerson,
                        AverageRating = c.AverageRating,
                        TotalReviews = c.TotalReviews,
                        LocalImagePath = c.LocalImagePath,
                        IsVegetarian = c.IsVegetarian,
                        IsVegan = c.IsVegan,
                        MealIncluded = c.MealIncluded
                    }).ToList()
                };

                if (item.Classes.Any())
                {
                    item.LowestPrice = item.Classes.Where(c => c.PricePerPerson.HasValue).Min(c => c.PricePerPerson);
                    item.HighestPrice = item.Classes.Where(c => c.PricePerPerson.HasValue).Max(c => c.PricePerPerson);
                }

                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    item.DistanceKm = CalculateDistance(request.Latitude.Value, request.Longitude.Value, s.Latitude, s.Longitude);
                }

                return item;
            }).ToList();

            if (request.Latitude.HasValue && request.Longitude.HasValue && string.IsNullOrEmpty(request.SortBy))
            {
                schoolItems = schoolItems.OrderBy(s => s.DistanceKm).ToList();
            }

            return new InternalSearchResult
            {
                Schools = schoolItems,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        public async Task<List<CookingSchool>> GetFeaturedSchoolsAsync(int count = 10)
        {
            return await _context.Schools
                .Include(s => s.Classes.Where(c => c.IsActive))
                .Where(s => s.IsActive && s.IsPremium)
                .OrderByDescending(s => s.AverageRating)
                .Take(count)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<CookingSchool>> GetNearbySchoolsAsync(double latitude, double longitude, double radiusKm = 50, int limit = 20)
        {
            var latDelta = radiusKm / 111.0;
            var lngDelta = radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180));

            var schools = await _context.Schools
                .Include(s => s.Classes.Where(c => c.IsActive))
                .Where(s => s.IsActive &&
                    s.Latitude >= latitude - latDelta && s.Latitude <= latitude + latDelta &&
                    s.Longitude >= longitude - lngDelta && s.Longitude <= longitude + lngDelta)
                .AsNoTracking()
                .ToListAsync();

            return schools
                .Select(s =>
                {
                    s.DistanceKm = CalculateDistance(latitude, longitude, s.Latitude, s.Longitude);
                    return s;
                })
                .Where(s => s.DistanceKm <= radiusKm)
                .OrderBy(s => s.DistanceKm)
                .Take(limit)
                .ToList();
        }

        public async Task<CookingSchool> CreateSchoolAsync(CookingSchool school)
        {
            school.Slug = GenerateSlug(school.Name, school.City);
            school.CreatedAt = DateTime.UtcNow;
            school.UpdatedAt = DateTime.UtcNow;

            _context.Schools.Add(school);
            await _context.SaveChangesAsync();

            return school;
        }

        public async Task<CookingSchool> UpdateSchoolAsync(CookingSchool school)
        {
            school.UpdatedAt = DateTime.UtcNow;
            _context.Schools.Update(school);
            await _context.SaveChangesAsync();

            return school;
        }

        public async Task<bool> DeleteSchoolAsync(int schoolId)
        {
            var school = await _context.Schools.FindAsync(schoolId);
            if (school == null) return false;

            school.IsActive = false;
            school.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task UpdateSchoolRatingAsync(int schoolId)
        {
            var school = await _context.Schools.FindAsync(schoolId);
            if (school == null) return;

            var reviews = await _context.Reviews
                .Where(r => r.SchoolId == schoolId && r.IsApproved)
                .ToListAsync();

            if (reviews.Any())
            {
                school.AverageRating = (decimal)reviews.Average(r => r.Rating);
                school.TotalReviews = reviews.Count;
            }
            else
            {
                school.AverageRating = null;
                school.TotalReviews = 0;
            }

            school.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<List<string>> GetAllCitiesAsync()
        {
            return await _context.Schools
                .Where(s => s.IsActive)
                .Select(s => s.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllCuisineTypesAsync()
        {
            return await _context.Classes
                .Where(c => c.IsActive)
                .Select(c => c.CuisineType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        public async Task<int> GetTotalSchoolCountAsync()
        {
            return await _context.Schools.CountAsync(s => s.IsActive);
        }

        public async Task<int> GetTotalClassCountAsync()
        {
            return await _context.Classes.CountAsync(c => c.IsActive);
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
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

            var baseSlug = slug;
            var counter = 1;
            while (_context.Schools.Any(s => s.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }
    }
}
