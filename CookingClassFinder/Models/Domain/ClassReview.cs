using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CookingClassFinder.Models.Domain
{
    /// <summary>
    /// Review for a specific cooking class (individual class experience)
    /// </summary>
    public class ClassReview
    {
        [Key]
        public int ClassReviewId { get; set; }

        [Required]
        public int ClassId { get; set; }

        public int? UserId { get; set; }

        // Overall rating
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        // Detailed ratings
        [Range(1, 5)]
        public int? InstructorRating { get; set; }

        [Range(1, 5)]
        public int? RecipeClarityRating { get; set; }

        [Range(1, 5)]
        public int? FacilitiesRating { get; set; }

        [Range(1, 5)]
        public int? ValueForMoneyRating { get; set; }

        [Range(1, 5)]
        public int? FoodQualityRating { get; set; } // How good was the final dish

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(5000)]
        public string? Content { get; set; }

        // Class Outcome
        public bool? DidCompleteRecipe { get; set; }
        public bool? WouldRecommend { get; set; }
        public bool? WouldTakeAgain { get; set; }

        // Difficulty feedback
        [Range(1, 5)]
        public int? PerceivedDifficulty { get; set; } // What they thought vs listed difficulty

        // Tips for future students
        [MaxLength(1000)]
        public string? TipsForFutureStudents { get; set; }

        // What they learned
        [MaxLength(500)]
        public string? FavoritePartOfClass { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ClassDate { get; set; }

        public bool IsVerifiedAttendance { get; set; } = false;
        public bool IsApproved { get; set; } = false;
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }

        public int HelpfulCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ClassId")]
        public virtual CookingClass Class { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Helper methods
        public string GetRatingStars()
        {
            return new string('★', Rating) + new string('☆', 5 - Rating);
        }

        public string GetCompletionResult()
        {
            if (!DidCompleteRecipe.HasValue) return "Not specified";
            return DidCompleteRecipe.Value ? "Completed the recipe successfully" : "Did not complete";
        }

        public string GetRecommendationText()
        {
            if (!WouldRecommend.HasValue) return "Not specified";
            return WouldRecommend.Value ? "Would recommend this class" : "Would not recommend";
        }

        public string GetTimeAgo()
        {
            DateTime baseDate = UserId == null && ClassDate.HasValue ? ClassDate.Value : CreatedAt;
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
