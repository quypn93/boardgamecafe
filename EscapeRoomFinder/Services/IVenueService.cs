using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Models.DTOs;

namespace EscapeRoomFinder.Services
{
    public interface IVenueService
    {
        // Search methods
        Task<List<VenueSearchResultDto>> SearchNearbyAsync(VenueSearchRequest request);
        Task<VenueSearchPagedResult> SearchVenuesPagedAsync(VenueSearchRequest request);
        Task<List<EscapeRoomVenue>> SearchByTextAsync(string query, int limit = 10);
        Task<List<VenueSearchResultDto>> FilterVenuesAsync(string? country, string? city, string? theme, int? difficulty, int? players, double? minRating);

        // Get methods
        Task<EscapeRoomVenue?> GetByIdAsync(int venueId);
        Task<EscapeRoomVenue?> GetBySlugAsync(string slug);
        Task<List<EscapeRoomVenue>> GetFeaturedVenuesAsync(int count = 10);
        Task<List<EscapeRoomVenue>> GetNearbyVenuesAsync(double latitude, double longitude, double radiusKm = 50, int limit = 20);

        // CRUD methods
        Task<EscapeRoomVenue> CreateVenueAsync(EscapeRoomVenue venue);
        Task<EscapeRoomVenue> UpdateVenueAsync(EscapeRoomVenue venue);
        Task<bool> DeleteVenueAsync(int venueId);
        Task UpdateVenueRatingAsync(int venueId);

        // Utility methods
        Task<List<string>> GetAllCitiesAsync();
        Task<List<string>> GetAllThemesAsync();
        Task<int> GetTotalVenueCountAsync();
        Task<int> GetTotalRoomCountAsync();
    }
}
