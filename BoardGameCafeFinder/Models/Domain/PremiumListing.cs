using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    public class PremiumListing
    {
        [Key]
        public int ListingId { get; set; }

        [Required]
        public int CafeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PlanType { get; set; } = "Basic"; // Basic ($50), Premium ($100), Featured ($200)

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal MonthlyFee { get; set; }

        // Features
        public bool FeaturedPlacement { get; set; } = false;
        public bool PhotoGallery { get; set; } = false;
        public bool EventListings { get; set; } = false;
        public bool GameInventoryManager { get; set; } = false;
        public bool AnalyticsDashboard { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CafeId")]
        public virtual Cafe Cafe { get; set; } = null!;

        // Helper methods
        public bool IsExpired()
        {
            return DateTime.UtcNow > EndDate;
        }

        public int GetDaysRemaining()
        {
            var span = EndDate - DateTime.UtcNow;
            return span.Days > 0 ? span.Days : 0;
        }
    }
}
