using CookingClassFinder.Models.Domain;
using CookingClassFinder.Models.DTOs;

namespace CookingClassFinder.Services
{
    public interface ISchoolService
    {
        // Search methods
        Task<List<SchoolSearchResultDto>> SearchNearbyAsync(SchoolSearchRequest request);
        Task<SchoolSearchPagedResult> SearchSchoolsPagedAsync(SchoolSearchRequest request);
        Task<List<CookingSchool>> SearchByTextAsync(string query, int limit = 10);
        Task<List<SchoolSearchResultDto>> FilterSchoolsAsync(string? country, string? city, string? cuisineType, string? difficultyLevel, decimal? maxPrice, double? minRating);

        // Get methods
        Task<CookingSchool?> GetByIdAsync(int schoolId);
        Task<CookingSchool?> GetBySlugAsync(string slug);
        Task<List<CookingSchool>> GetFeaturedSchoolsAsync(int count = 10);
        Task<List<CookingSchool>> GetNearbySchoolsAsync(double latitude, double longitude, double radiusKm = 50, int limit = 20);

        // CRUD methods
        Task<CookingSchool> CreateSchoolAsync(CookingSchool school);
        Task<CookingSchool> UpdateSchoolAsync(CookingSchool school);
        Task<bool> DeleteSchoolAsync(int schoolId);
        Task UpdateSchoolRatingAsync(int schoolId);

        // Utility methods
        Task<List<string>> GetAllCitiesAsync();
        Task<List<string>> GetAllCuisineTypesAsync();
        Task<int> GetTotalSchoolCountAsync();
        Task<int> GetTotalClassCountAsync();
    }
}
