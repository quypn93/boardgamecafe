using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    /// <summary>
    /// Many-to-Many relationship between Cafes and BoardGames
    /// Tracks which games are available at which cafés
    /// </summary>
    public class CafeGame
    {
        [Key]
        public int CafeGameId { get; set; }

        [Required]
        public int CafeId { get; set; }

        [Required]
        public int GameId { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? RentalPrice { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime? LastVerified { get; set; }

        public int? VerifiedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CafeId")]
        public virtual Cafe Cafe { get; set; } = null!;

        [ForeignKey("GameId")]
        public virtual BoardGame Game { get; set; } = null!;

        [ForeignKey("VerifiedByUserId")]
        public virtual User? VerifiedBy { get; set; }
    }
}
