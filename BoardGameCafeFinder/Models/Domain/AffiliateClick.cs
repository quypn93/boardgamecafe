using System.ComponentModel.DataAnnotations;

namespace BoardGameCafeFinder.Models.Domain
{
    public class AffiliateClick
    {
        [Key]
        public int ClickId { get; set; }

        public int GameId { get; set; }

        public int? CafeId { get; set; }

        public int? UserId { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(500)]
        public string? Referrer { get; set; }

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual BoardGame? Game { get; set; }
        public virtual Cafe? Cafe { get; set; }
        public virtual User? User { get; set; }
    }
}
