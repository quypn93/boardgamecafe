using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EscapeRoomFinder.Models.Domain
{
    /// <summary>
    /// Review for a specific escape room (individual room experience)
    /// </summary>
    public class RoomReview
    {
        [Key]
        public int RoomReviewId { get; set; }

        [Required]
        public int RoomId { get; set; }

        public int? UserId { get; set; }

        // Overall rating
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        // Detailed ratings
        [Range(1, 5)]
        public int? PuzzleQualityRating { get; set; }

        [Range(1, 5)]
        public int? ThemeImmersionRating { get; set; }

        [Range(1, 5)]
        public int? StaffRating { get; set; }

        [Range(1, 5)]
        public int? ValueForMoneyRating { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(5000)]
        public string? Content { get; set; }

        // Game Result
        public bool? DidEscape { get; set; }
        public int? TimeRemainingSeconds { get; set; } // Time left when escaped
        public int? HintsUsed { get; set; }
        public int? TeamSize { get; set; }

        // Difficulty feedback
        [Range(1, 5)]
        public int? PerceivedDifficulty { get; set; } // What they thought vs listed difficulty

        [DataType(DataType.Date)]
        public DateTime? PlayDate { get; set; }

        public bool IsVerifiedPlay { get; set; } = false;
        public bool IsApproved { get; set; } = false;
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }

        public int HelpfulCount { get; set; } = 0;

        // Spoiler warning
        public bool ContainsSpoilers { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("RoomId")]
        public virtual EscapeRoom Room { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Helper methods
        public string GetRatingStars()
        {
            return new string('★', Rating) + new string('☆', 5 - Rating);
        }

        public string GetEscapeResult()
        {
            if (!DidEscape.HasValue) return "Not specified";
            if (DidEscape.Value)
            {
                if (TimeRemainingSeconds.HasValue)
                {
                    var minutes = TimeRemainingSeconds.Value / 60;
                    var seconds = TimeRemainingSeconds.Value % 60;
                    return $"Escaped with {minutes}:{seconds:D2} remaining";
                }
                return "Escaped!";
            }
            return "Did not escape";
        }

        public string GetTimeAgo()
        {
            DateTime baseDate = UserId == null && PlayDate.HasValue ? PlayDate.Value : CreatedAt;
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
