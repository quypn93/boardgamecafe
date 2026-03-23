using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VRArcadeFinder.Models.Domain
{
    public class VRGame
    {
        [Key]
        public int GameId { get; set; }

        // Alias for views
        [NotMapped]
        public int Id => GameId;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Developer { get; set; }

        [MaxLength(200)]
        public string? Publisher { get; set; }

        public int? MinPlayers { get; set; }

        public int? MaxPlayers { get; set; }

        public int? PlaytimeMinutes { get; set; }

        [MaxLength(10)]
        public string? AgeRating { get; set; } // e.g., "10+", "13+", "18+"

        [MaxLength(100)]
        public string? Genre { get; set; } // e.g., "Action", "Adventure", "Horror", "Simulation", "Sports"

        [MaxLength(100)]
        public string? VRPlatform { get; set; } // e.g., "Meta Quest", "HTC Vive", "PlayStation VR", "Valve Index"

        public bool RequiresRoomScale { get; set; } = false;

        public bool IsMultiplayer { get; set; } = false;

        public bool IsCoOp { get; set; } = false;

        [MaxLength(50)]
        public string? IntensityLevel { get; set; } // "Low", "Medium", "High", "Extreme"

        [Column(TypeName = "decimal(3, 2)")]
        public decimal? Rating { get; set; } // 1.0 to 5.0 scale

        public int? SteamAppId { get; set; } // Steam App ID for VR games

        public int? OculusAppId { get; set; } // Meta/Oculus App ID

        [MaxLength(500)]
        [Url]
        public string? ImageUrl { get; set; }

        [MaxLength(500)]
        [Url]
        public string? TrailerUrl { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; } // e.g., "Escape Room", "Shooter", "Racing", "Puzzle", "Fitness"

        [MaxLength(1000)]
        [Url]
        public string? SteamUrl { get; set; }

        [MaxLength(1000)]
        [Url]
        public string? OculusUrl { get; set; }

        [MaxLength(2000)]
        [Url]
        public string? SourceUrl { get; set; } // URL to the game on the arcade's website

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? Price { get; set; } // Local price at the arcade

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<ArcadeGame> ArcadeGames { get; set; } = new List<ArcadeGame>();

        // Helper method
        public string GetPlayerRange()
        {
            if (MinPlayers == null && MaxPlayers == null)
                return "Unknown";

            if (MinPlayers == MaxPlayers)
                return $"{MinPlayers} players";

            var maxDisplay = MaxPlayers.HasValue ? MaxPlayers.Value.ToString() : "+";
            return $"{MinPlayers ?? 1}-{maxDisplay} players";
        }

        public string GetPlaytime()
        {
            if (PlaytimeMinutes == null)
                return "Unknown";

            if (PlaytimeMinutes < 60)
                return $"{PlaytimeMinutes} min";

            var hours = PlaytimeMinutes / 60;
            var minutes = PlaytimeMinutes % 60;

            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        public string GetIntensityBadgeClass()
        {
            return IntensityLevel?.ToLower() switch
            {
                "low" => "bg-success",
                "medium" => "bg-warning",
                "high" => "bg-orange",
                "extreme" => "bg-danger",
                _ => "bg-secondary"
            };
        }
    }
}
