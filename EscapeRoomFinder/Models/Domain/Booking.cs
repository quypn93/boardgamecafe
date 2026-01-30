using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Tracks booking referrals to escape room venues
    /// </summary>
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        public int? UserId { get; set; }

        [Required]
        public int RoomId { get; set; }

        public int VenueId { get; set; }

        // Booking details
        [Required]
        public DateTime BookingDate { get; set; }

        [MaxLength(50)]
        public string? TimeSlot { get; set; }

        public int? PartySize { get; set; }

        // Referral tracking
        [MaxLength(100)]
        public string? ReferralSource { get; set; } // organic, google, affiliate, etc.

        [MaxLength(500)]
        public string? AffiliateCode { get; set; }

        [MaxLength(500)]
        public string? ExternalBookingId { get; set; } // Booking platform reference

        [MaxLength(500)]
        [Url]
        public string? BookingUrl { get; set; }

        // Status
        [MaxLength(50)]
        public string Status { get; set; } = "clicked"; // clicked, booked, completed, cancelled

        // Commission tracking
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? BookingValue { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? CommissionAmount { get; set; }

        public bool CommissionPaid { get; set; } = false;

        // User data (for non-logged in users)
        [MaxLength(100)]
        [EmailAddress]
        public string? GuestEmail { get; set; }

        [MaxLength(100)]
        public string? GuestName { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? BookedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("RoomId")]
        public virtual EscapeRoom Room { get; set; } = null!;

        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue Venue { get; set; } = null!;
    }
}
