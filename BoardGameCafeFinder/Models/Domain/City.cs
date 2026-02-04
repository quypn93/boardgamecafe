using System.ComponentModel.DataAnnotations;

namespace BoardGameCafeFinder.Models.Domain
{
    public class City
    {
        [Key]
        public int CityId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Country { get; set; }

        [Required]
        [MaxLength(20)]
        public string Region { get; set; } = "US"; // US, International

        /// <summary>
        /// Number of times this city has been crawled
        /// </summary>
        public int CrawlCount { get; set; } = 0;

        /// <summary>
        /// Last time this city was crawled
        /// </summary>
        public DateTime? LastCrawledAt { get; set; }

        /// <summary>
        /// Status of the last crawl: Success, Failed, Partial
        /// </summary>
        [MaxLength(20)]
        public string? LastCrawlStatus { get; set; }

        /// <summary>
        /// When to retry crawling this city (set after failure)
        /// </summary>
        public DateTime? NextCrawlAt { get; set; }

        /// <summary>
        /// Whether this city should be included in auto crawl
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Maximum results to fetch per crawl for this city
        /// </summary>
        public int MaxResults { get; set; } = 15;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<CrawlHistory> CrawlHistories { get; set; } = new List<CrawlHistory>();
    }
}
