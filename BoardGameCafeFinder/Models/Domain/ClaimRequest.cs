using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    /// <summary>
    /// Represents a claim request for a premium listing
    /// </summary>
    public class ClaimRequest
    {
        [Key]
        public int ClaimRequestId { get; set; }

        // Cafe being claimed
        public int CafeId { get; set; }
        [ForeignKey("CafeId")]
        public virtual Cafe? Cafe { get; set; }

        // User who made the claim (if logged in)
        public int? UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Contact Information
        [Required]
        [MaxLength(200)]
        public string ContactName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string ContactEmail { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [MaxLength(50)]
        public string ContactRole { get; set; } = "Owner"; // Owner, Manager, Marketing, Other

        // Plan Details
        [Required]
        [MaxLength(50)]
        public string PlanType { get; set; } = "Premium"; // Basic, Premium, Featured

        public int DurationMonths { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Verification
        [MaxLength(50)]
        public string VerificationMethod { get; set; } = "email"; // phone, email, document

        public bool IsVerified { get; set; } = false;
        public DateTime? VerifiedAt { get; set; }

        // Payment Status
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "pending"; // pending, processing, completed, failed, refunded

        [MaxLength(500)]
        public string? StripeSessionId { get; set; }

        [MaxLength(500)]
        public string? StripePaymentIntentId { get; set; }

        [MaxLength(500)]
        public string? StripeCustomerId { get; set; }

        public DateTime? PaidAt { get; set; }

        // Resulting Listing (created after payment)
        public int? PremiumListingId { get; set; }
        [ForeignKey("PremiumListingId")]
        public virtual PremiumListing? PremiumListing { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Admin notes
        [MaxLength(1000)]
        public string? AdminNotes { get; set; }

        // Helper properties
        [NotMapped]
        public bool IsPaid => PaymentStatus == "completed";

        [NotMapped]
        public string StatusBadgeClass => PaymentStatus switch
        {
            "completed" => "bg-success",
            "pending" => "bg-warning",
            "processing" => "bg-info",
            "failed" => "bg-danger",
            "refunded" => "bg-secondary",
            _ => "bg-secondary"
        };
    }
}
