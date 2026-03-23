using System.ComponentModel.DataAnnotations;

namespace VRArcadeFinder.Models.Domain
{
    public class AffiliateClick
    {
        [Key]
        public int ClickId { get; set; }

        public int GameId { get; set; }

        public int? ArcadeId { get; set; }

        public int? UserId { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        [MaxLength(500)]
        public string? Referrer { get; set; }

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual VRGame? Game { get; set; }
        public virtual Arcade? Arcade { get; set; }
        public virtual User? User { get; set; }
    }
}
