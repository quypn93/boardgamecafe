using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CookingClassFinder.Models.Domain
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        // Alias for views/controllers
        [NotMapped]
        public int Id => CityId;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Slug { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = "United States";

        [MaxLength(50)]
        public string Region { get; set; } = "US"; // "US" or "International"

        // Location
        [Column(TypeName = "decimal(10, 8)")]
        public double? Latitude { get; set; }

        [Column(TypeName = "decimal(11, 8)")]
        public double? Longitude { get; set; }

        // City guide content
        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? GuideContent { get; set; }

        public bool IsFeatured { get; set; } = false;

        public int SchoolCount { get; set; } = 0;

        // Crawl settings
        public int CrawlCount { get; set; } = 0;

        public DateTime? LastCrawledAt { get; set; }

        [MaxLength(50)]
        public string? LastCrawlStatus { get; set; } // "Success", "Failed", "InProgress"

        public DateTime? NextCrawlAt { get; set; } // For retry scheduling

        public bool IsActive { get; set; } = true;

        [Range(5, 50)]
        public int MaxResults { get; set; } = 15;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<CrawlHistory> CrawlHistories { get; set; } = new List<CrawlHistory>();
    }
}
