using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Represents an individual escape room within a venue
    /// </summary>
    public class EscapeRoom
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        public int VenueId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        // Room Theme/Genre
        [Required]
        [MaxLength(100)]
        public string Theme { get; set; } = "Mystery"; // Horror, Adventure, Mystery, Sci-Fi, Fantasy, Historical, Comedy, Crime, etc.

        [MaxLength(500)]
        public string? ThemeDescription { get; set; } // Short backstory/premise

        // Difficulty (1-5 scale)
        [Required]
        [Range(1, 5)]
        public int Difficulty { get; set; } = 3;

        // Player count
        [Required]
        [Range(1, 20)]
        public int MinPlayers { get; set; } = 2;

        [Required]
        [Range(1, 20)]
        public int MaxPlayers { get; set; } = 6;

        public int? RecommendedPlayers { get; set; } // Optimal team size

        // Time
        [Required]
        [Range(15, 180)]
        public int DurationMinutes { get; set; } = 60;

        // Pricing
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PricePerPerson { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PricePerGroup { get; set; }

        // Success Rate (percentage of teams that escape)
        [Column(TypeName = "decimal(5, 2)")]
        [Range(0, 100)]
        public decimal? SuccessRate { get; set; }

        // Room Features
        public bool IsScaryOrIntense { get; set; } = false;
        public bool RequiresPhysicalActivity { get; set; } = false;
        public bool IsWheelchairAccessible { get; set; } = false;
        public bool HasJumpscares { get; set; } = false;
        public bool RequiresCrawling { get; set; } = false;
        public bool IsKidFriendly { get; set; } = true;
        public bool HasActors { get; set; } = false; // Live actors in the room
        public bool UsesVR { get; set; } = false; // VR elements
        public bool HasHighTechPuzzles { get; set; } = false;

        // Age requirements
        public int? MinAge { get; set; }
        public int? MinAgeWithAdult { get; set; }

        // Languages
        [MaxLength(200)]
        public string? AvailableLanguages { get; set; } // Comma-separated: "English, Spanish, French"

        // External IDs
        [MaxLength(200)]
        public string? ExternalId { get; set; } // Booking platform ID

        [MaxLength(500)]
        [Url]
        public string? BookingUrl { get; set; }

        // Images
        [MaxLength(500)]
        public string? LocalImagePath { get; set; }

        [MaxLength(500)]
        [Url]
        public string? ImageUrl { get; set; }

        // Ratings & Stats
        [Column(TypeName = "decimal(3, 2)")]
        public decimal? AverageRating { get; set; }

        public int TotalReviews { get; set; } = 0;
        public int TotalPlays { get; set; } = 0;

        // Status
        public bool IsActive { get; set; } = true;
        public bool IsNew { get; set; } = false; // Flag for newly opened rooms

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? OpenedAt { get; set; } // When the room first opened

        // SEO
        [Required]
        [MaxLength(300)]
        public string Slug { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue Venue { get; set; } = null!;

        public virtual ICollection<RoomReview> Reviews { get; set; } = new List<RoomReview>();
        public virtual ICollection<RoomPhoto> Photos { get; set; } = new List<RoomPhoto>();

        // Helper methods
        public string GetDifficultyText()
        {
            return Difficulty switch
            {
                1 => "Very Easy",
                2 => "Easy",
                3 => "Medium",
                4 => "Hard",
                5 => "Expert",
                _ => "Unknown"
            };
        }

        public string GetDifficultyBadgeClass()
        {
            return Difficulty switch
            {
                1 => "bg-success",
                2 => "bg-info",
                3 => "bg-warning",
                4 => "bg-orange",
                5 => "bg-danger",
                _ => "bg-secondary"
            };
        }

        public string GetPlayerRange()
        {
            if (MinPlayers == MaxPlayers)
                return $"{MinPlayers} players";
            return $"{MinPlayers}-{MaxPlayers} players";
        }

        public string GetPriceDisplay()
        {
            if (PricePerPerson.HasValue)
                return $"${PricePerPerson:F0}/person";
            if (PricePerGroup.HasValue)
                return $"${PricePerGroup:F0}/group";
            return "Contact for pricing";
        }

        public string GetSuccessRateDisplay()
        {
            if (SuccessRate.HasValue)
                return $"{SuccessRate:F0}% escape rate";
            return "No data";
        }

        public List<string> GetFeatureTags()
        {
            var tags = new List<string>();
            if (IsScaryOrIntense) tags.Add("Scary");
            if (HasJumpscares) tags.Add("Jump Scares");
            if (HasActors) tags.Add("Live Actors");
            if (UsesVR) tags.Add("VR Elements");
            if (HasHighTechPuzzles) tags.Add("High-Tech");
            if (RequiresPhysicalActivity) tags.Add("Physical");
            if (RequiresCrawling) tags.Add("Crawling Required");
            if (IsKidFriendly) tags.Add("Kid Friendly");
            if (IsWheelchairAccessible) tags.Add("Accessible");
            return tags;
        }
    }
}
