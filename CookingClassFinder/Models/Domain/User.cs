using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CookingClassFinder.Models.Domain
{
    /// <summary>
    /// Extended user model based on ASP.NET Core Identity
    /// </summary>
    public class User : IdentityUser<int>
    {
        // Profile Information
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [MaxLength(1000)]
        public string? Bio { get; set; }

        [MaxLength(500)]
        [Url]
        public string? AvatarUrl { get; set; }

        // Location (optional for personalized recommendations)
        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        // Preferences - stored as JSON array
        public string? FavoriteCuisines { get; set; } // Italian, French, Asian, etc.

        // Dietary preferences
        public string? DietaryPreferences { get; set; } // Vegetarian, Vegan, Gluten-Free, etc.

        // Stats
        public int TotalReviews { get; set; } = 0;
        public int TotalClassesTaken { get; set; } = 0;
        public int TotalRecipesLearned { get; set; } = 0;
        public int ReputationScore { get; set; } = 0;

        // Account Type
        public bool IsSchoolOwner { get; set; } = false;
        public int? SchoolId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

        // Navigation Properties
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
        public virtual ICollection<ClassReview> ClassReviews { get; set; } = new List<ClassReview>();
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<Photo> Photos { get; set; } = new List<Photo>();
        public virtual ICollection<CookingSchool> CreatedSchools { get; set; } = new List<CookingSchool>();

        // Helper method
        public string GetFullName()
        {
            if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                return $"{FirstName} {LastName}";

            return DisplayName ?? UserName ?? Email ?? "Anonymous";
        }

        public string GetCookingLevel()
        {
            if (TotalClassesTaken >= 50) return "Master Chef";
            if (TotalClassesTaken >= 25) return "Experienced Cook";
            if (TotalClassesTaken >= 10) return "Home Chef";
            if (TotalClassesTaken >= 5) return "Cooking Enthusiast";
            if (TotalClassesTaken >= 1) return "Beginner";
            return "New Member";
        }
    }
}
