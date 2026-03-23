using VRArcadeFinder.Models.Domain;

namespace VRArcadeFinder.Models.DTOs
{
    public class MapViewModel
    {
        public double? InitialLat { get; set; }
        public double? InitialLng { get; set; }
        public int? InitialZoom { get; set; }
        public string? SelectedCity { get; set; }
        public string? SelectedCountry { get; set; }
        public List<string> Countries { get; set; } = new List<string>();
        public List<string> Cities { get; set; } = new List<string>();
        public List<string> VRPlatforms { get; set; } = new List<string>();
        public List<string> GameCategories { get; set; } = new List<string>();

        // For Map View
        public string? City { get; set; }
        public double? CenterLat { get; set; }
        public double? CenterLng { get; set; }
        public int? Zoom { get; set; }
        public IEnumerable<MapArcadeDto>? Arcades { get; set; }
    }

    public class MapArcadeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public decimal Rating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsPremium { get; set; }
        public double? Distance { get; set; }
    }
}
