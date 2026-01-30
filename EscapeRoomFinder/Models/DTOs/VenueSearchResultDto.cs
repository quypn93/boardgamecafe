namespace EscapeRoomFinder.Models.DTOs
{
    // Single venue result for API
    public class VenueSearchResultDto
    {
        public int VenueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? LocalImagePath { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalRooms { get; set; }
        public bool IsPremium { get; set; }
        public double DistanceKm { get; set; }
    }

    // Paginated search result (internal use)
    public class VenueSearchPagedResult
    {
        public List<VenueListItemDto> Venues { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    public class VenueListItemDto
    {
        public int VenueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? LocalImagePath { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalRooms { get; set; }
        public bool IsVerified { get; set; }
        public bool IsPremium { get; set; }
        public double DistanceKm { get; set; }

        // Room summary
        public List<RoomSummaryDto> Rooms { get; set; } = new();
        public string? MostPopularTheme { get; set; }
        public int? LowestDifficulty { get; set; }
        public int? HighestDifficulty { get; set; }
    }

    public class RoomSummaryDto
    {
        public int RoomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public int Difficulty { get; set; }
        public int MinPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public int DurationMinutes { get; set; }
        public decimal? PricePerPerson { get; set; }
        public decimal? SuccessRate { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string? LocalImagePath { get; set; }
    }
}
