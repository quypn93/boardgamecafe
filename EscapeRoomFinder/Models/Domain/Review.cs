using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Review for an escape room venue (overall experience)
    /// </summary>
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        [Required]
        public int VenueId { get; set; }

        public int? UserId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(5000)]
        public string? Content { get; set; }

        [DataType(DataType.Date)]
        public DateTime? VisitDate { get; set; }

        public bool IsVerifiedVisit { get; set; } = false;

        public bool IsApproved { get; set; } = false;

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        public int HelpfulCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("VenueId")]
        public virtual EscapeRoomVenue Venue { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Helper methods
        public string GetRatingStars()
        {
            return new string('★', Rating) + new string('☆', 5 - Rating);
        }

        public string GetTimeAgo()
        {
            DateTime baseDate = UserId == null && VisitDate.HasValue ? VisitDate.Value : CreatedAt;
            var span = DateTime.UtcNow - baseDate;

            if (span.TotalDays > 365)
                return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) > 1 ? "s" : "")} ago";
            if (span.TotalDays > 30)
                return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) > 1 ? "s" : "")} ago";
            if (span.TotalDays > 1)
                return $"{(int)span.TotalDays} day{((int)span.TotalDays > 1 ? "s" : "")} ago";
            if (span.TotalHours > 1)
                return $"{(int)span.TotalHours} hour{((int)span.TotalHours > 1 ? "s" : "")} ago";
            if (span.TotalMinutes > 1)
                return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes > 1 ? "s" : "")} ago";

            return "just now";
        }
    }
}
