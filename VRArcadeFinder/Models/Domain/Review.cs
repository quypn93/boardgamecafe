using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VRArcadeFinder.Models.Domain
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        // Alias for views
        [NotMapped]
        public int Id => ReviewId;

        [Required]
        public int ArcadeId { get; set; }

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

        /// <summary>
        /// Whether the review has been approved by admin (if approval is required)
        /// </summary>
        public bool IsApproved { get; set; } = false;

        /// <summary>
        /// Date when the review was approved
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// Admin who approved the review
        /// </summary>
        public int? ApprovedByUserId { get; set; }

        public int HelpfulCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ArcadeId")]
        public virtual Arcade Arcade { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Helper method
        public string GetRatingStars()
        {
            return new string('*', Rating);
        }

        public string GetTimeAgo()
        {
            // If UserId is null (crawled review from Google), use VisitDate; otherwise use CreatedAt
            DateTime baseDate;
            if (UserId == null)
            {
                if (VisitDate.HasValue)
                {
                    baseDate = VisitDate.Value;
                }
                else
                {
                    return "";
                }
            } else
            {
                baseDate = CreatedAt;
            }
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
