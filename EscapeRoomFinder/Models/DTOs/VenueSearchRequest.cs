namespace EscapeRoomFinder.Models.DTOs
{
    public class VenueSearchRequest
    {
        public string? Query { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public string? Theme { get; set; } // Filter by room theme
        public int? MinDifficulty { get; set; }
        public int? MaxDifficulty { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public bool? HasHorrorRooms { get; set; }
        public bool? IsKidFriendly { get; set; }
        public bool? IsWheelchairAccessible { get; set; }
        public string? SortBy { get; set; } // rating, distance, name, rooms_count
        public bool SortDescending { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
