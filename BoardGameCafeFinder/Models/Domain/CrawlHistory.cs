using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardGameCafeFinder.Models.Domain
{
    public class CrawlHistory
    {
        [Key]
        public int CrawlHistoryId { get; set; }

        [Required]
        public int CityId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Status: Success, Failed, Partial, InProgress
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "InProgress";

        /// <summary>
        /// Total number of cafes found during crawl
        /// </summary>
        public int CafesFound { get; set; } = 0;

        /// <summary>
        /// Number of new cafes added to database
        /// </summary>
        public int CafesAdded { get; set; } = 0;

        /// <summary>
        /// Number of existing cafes updated
        /// </summary>
        public int CafesUpdated { get; set; } = 0;

        /// <summary>
        /// Error message if crawl failed
        /// </summary>
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        // Navigation
        [ForeignKey("CityId")]
        public virtual City? City { get; set; }
    }
}
