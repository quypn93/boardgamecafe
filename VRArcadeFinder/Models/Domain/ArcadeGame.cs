using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VRArcadeFinder.Models.Domain
{
    /// <summary>
    /// Many-to-Many relationship between Arcades and VRGames
    /// Tracks which games are available at which arcades
    /// </summary>
    public class ArcadeGame
    {
        [Key]
        public int ArcadeGameId { get; set; }

        [Required]
        public int ArcadeId { get; set; }

        [Required]
        public int GameId { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int Quantity { get; set; } = 1; // Number of stations with this game

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PricePerSession { get; set; } // Price per session/hour

        public int? SessionDurationMinutes { get; set; } // Typical session duration

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime? LastVerified { get; set; }

        public int? VerifiedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ArcadeId")]
        public virtual Arcade Arcade { get; set; } = null!;

        [ForeignKey("GameId")]
        public virtual VRGame Game { get; set; } = null!;

        [ForeignKey("VerifiedByUserId")]
        public virtual User? VerifiedBy { get; set; }
    }
}
