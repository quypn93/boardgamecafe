using System.ComponentModel.DataAnnotations;

namespace EscapeRoomFinder.Models.Domain
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Country { get; set; } = "United States";

        [MaxLength(50)]
        public string Region { get; set; } = "US"; // "US" or "International"

        public int CrawlCount { get; set; } = 0;

        public DateTime? LastCrawledAt { get; set; }

        [MaxLength(50)]
        public string? LastCrawlStatus { get; set; } // "Success", "Failed", "InProgress"

        public DateTime? NextCrawlAt { get; set; } // For retry scheduling

        public bool IsActive { get; set; } = true;

        [Range(5, 50)]
        public int MaxResults { get; set; } = 15;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<CrawlHistory> CrawlHistories { get; set; } = new List<CrawlHistory>();
    }
}
