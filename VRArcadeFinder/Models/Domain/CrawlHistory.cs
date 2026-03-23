using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VRArcadeFinder.Models.Domain
{
    public class CrawlHistory
    {
        [Key]
        public int CrawlHistoryId { get; set; }

        // Alias for views
        [NotMapped]
        public int Id => CrawlHistoryId;

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
        /// Total number of arcades found during crawl
        /// </summary>
        public int ArcadesFound { get; set; } = 0;

        // Alias for views
        [NotMapped]
        public int TotalFound => ArcadesFound;

        /// <summary>
        /// Number of new arcades added to database
        /// </summary>
        public int ArcadesAdded { get; set; } = 0;

        // Alias for views
        [NotMapped]
        public int NewArcades => ArcadesAdded;

        /// <summary>
        /// Number of existing arcades updated
        /// </summary>
        public int ArcadesUpdated { get; set; } = 0;

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
