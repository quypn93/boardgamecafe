using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    /// <summary>
    /// Represents an invoice for premium listing payments
    /// </summary>
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }

        // Invoice Number (e.g., INV-2026-0001)
        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        // Related Entities
        public int? ClaimRequestId { get; set; }
        [ForeignKey("ClaimRequestId")]
        public virtual ClaimRequest? ClaimRequest { get; set; }

        public int? PremiumListingId { get; set; }
        [ForeignKey("PremiumListingId")]
        public virtual PremiumListing? PremiumListing { get; set; }

        public int CafeId { get; set; }
        [ForeignKey("CafeId")]
        public virtual Cafe? Cafe { get; set; }

        // Billing Information
        [Required]
        [MaxLength(200)]
        public string BillingName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string BillingEmail { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? BillingAddress { get; set; }

        [MaxLength(100)]
        public string? BillingCity { get; set; }

        [MaxLength(100)]
        public string? BillingCountry { get; set; }

        [MaxLength(20)]
        public string? BillingPostalCode { get; set; }

        // Invoice Details
        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PlanType { get; set; } = string.Empty;

        public int PeriodMonths { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Amounts
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; } = "USD";

        // Payment Information
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "pending"; // pending, paid, failed, refunded

        [MaxLength(50)]
        public string PaymentMethod { get; set; } = "stripe"; // stripe, paypal, manual

        [MaxLength(500)]
        public string? StripePaymentIntentId { get; set; }

        [MaxLength(500)]
        public string? StripeInvoiceId { get; set; }

        public DateTime? PaidAt { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }

        // Refund info
        public bool IsRefunded { get; set; }
        public DateTime? RefundedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RefundAmount { get; set; }

        [MaxLength(500)]
        public string? RefundReason { get; set; }

        // Helper properties
        [NotMapped]
        public bool IsPaid => PaymentStatus == "paid";

        [NotMapped]
        public string StatusBadgeClass => PaymentStatus switch
        {
            "paid" => "bg-success",
            "pending" => "bg-warning",
            "failed" => "bg-danger",
            "refunded" => "bg-secondary",
            _ => "bg-secondary"
        };

        // Generate invoice number
        public static string GenerateInvoiceNumber(int sequence)
        {
            return $"INV-{DateTime.UtcNow.Year}-{sequence:D5}";
        }
    }
}
