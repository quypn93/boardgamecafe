using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    public class CrawlHistory
    {
        [Key]
        public int CrawlHistoryId { get; set; }

        public int CityId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "InProgress"; // "Success", "Failed", "InProgress"

        public int VenuesFound { get; set; } = 0;

        public int VenuesAdded { get; set; } = 0;

        public int VenuesUpdated { get; set; } = 0;

        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }

        // Navigation
        [ForeignKey("CityId")]
        public virtual City? City { get; set; }
    }
}
