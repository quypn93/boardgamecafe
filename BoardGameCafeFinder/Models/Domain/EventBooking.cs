using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    public class EventBooking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        public int UserId { get; set; }

        public int NumberOfSeats { get; set; } = 1;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Confirmed"; // Confirmed, Cancelled, Waitlist

        [Required]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Refunded

        [Column(TypeName = "decimal(10, 2)")]
        public decimal TotalAmount { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        public DateTime? CancellationDate { get; set; }

        // Navigation Properties
        [ForeignKey("EventId")]
        public virtual Event Event { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        // Helper methods
        public bool IsActive()
        {
            return Status == "Confirmed" && Event.IsUpcoming();
        }

        public bool CanCancel()
        {
            // Allow cancellation up to 24 hours before event
            return IsActive() && Event.StartDateTime.Subtract(DateTime.UtcNow).TotalHours > 24;
        }
    }
}
