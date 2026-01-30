using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public int? ClaimRequestId { get; set; }
        public int? VenueId { get; set; }
        public int? UserId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "USD";

        [MaxLength(50)]
        public string Status { get; set; } = "pending"; // pending, paid, failed, refunded

        [MaxLength(500)]
        public string? Description { get; set; }

        // Payment details
        [MaxLength(50)]
        public string? PaymentMethod { get; set; } // stripe, paypal, bank_transfer

        [MaxLength(500)]
        public string? PaymentReference { get; set; }

        public DateTime? PaidAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }

        // Navigation
        [ForeignKey("ClaimRequestId")]
        public virtual ClaimRequest? ClaimRequest { get; set; }

        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue? Venue { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
