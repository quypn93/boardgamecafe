using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    public class BoardGame
    {
        [Key]
        public int GameId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? Publisher { get; set; }

        public int? MinPlayers { get; set; }

        public int? MaxPlayers { get; set; }

        public int? PlaytimeMinutes { get; set; }

        [MaxLength(10)]
        public string? AgeRating { get; set; } // e.g., "10+", "13+", "18+"

        [Column(TypeName = "decimal(3, 2)")]
        public decimal? Complexity { get; set; } // 1.0 to 5.0 scale

        public int? BGGId { get; set; } // BoardGameGeek ID

        [MaxLength(500)]
        [Url]
        public string? ImageUrl { get; set; }

        [MaxLength(1000)]
        [Url]
        public string? AmazonAffiliateUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<CafeGame> CafeGames { get; set; } = new List<CafeGame>();

        // Helper method
        public string GetPlayerRange()
        {
            if (MinPlayers == null && MaxPlayers == null)
                return "Unknown";

            if (MinPlayers == MaxPlayers)
                return $"{MinPlayers} players";

            var maxDisplay = MaxPlayers.HasValue ? MaxPlayers.Value.ToString() : "∞";
            return $"{MinPlayers ?? 0}-{maxDisplay} players";
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
    }
}
