using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Tracks affiliate link clicks for analytics
    /// </summary>
    public class AffiliateClick
    {
        [Key]
        public int ClickId { get; set; }

        public int? VenueId { get; set; }
        public int? RoomId { get; set; }

        [MaxLength(100)]
        public string? LinkType { get; set; } // website, booking, phone, map

        [MaxLength(500)]
        public string? DestinationUrl { get; set; }

        [MaxLength(100)]
        public string? ReferrerPage { get; set; } // Which page the click came from

        // User tracking
        public int? UserId { get; set; }

        [MaxLength(100)]
        public string? SessionId { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        // Location data
        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue? Venue { get; set; }

        [ForeignKey("RoomId")]
        public virtual EscapeRoom? Room { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
