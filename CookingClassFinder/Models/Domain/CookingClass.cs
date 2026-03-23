using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CookingClassFinder.Models.Domain
{
    /// <summary>
    /// Represents an individual cooking class offered by a school
    /// </summary>
    public class CookingClass
    {
        [Key]
        public int ClassId { get; set; }

        [Required]
        public int SchoolId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        // Cuisine Type
        [Required]
        [MaxLength(100)]
        public string CuisineType { get; set; } = CuisineTypes.Other; // Italian, French, Asian, etc.

        [MaxLength(500)]
        public string? CuisineDescription { get; set; } // What dishes will be prepared

        // Difficulty Level
        [Required]
        [MaxLength(50)]
        public string DifficultyLevel { get; set; } = DifficultyLevels.AllLevels; // Beginner, Intermediate, Advanced, AllLevels

        // Class size
        [Required]
        [Range(1, 50)]
        public int MinStudents { get; set; } = 1;

        [Required]
        [Range(1, 50)]
        public int MaxStudents { get; set; } = 12;

        // Duration
        [Required]
        [Range(30, 480)]
        public int DurationMinutes { get; set; } = 120; // Typical 2-3 hour class

        // Pricing
        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PricePerPerson { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? PriceForPrivateGroup { get; set; }

        // Class Features
        public bool IngredientsProvided { get; set; } = true;
        public bool MealIncluded { get; set; } = true; // Students eat what they cook
        public bool TakeHomeRecipes { get; set; } = true;
        public bool WineParingIncluded { get; set; } = false;
        public bool ApronProvided { get; set; } = true;
        public bool HandsOnCooking { get; set; } = true; // vs demonstration only
        public bool IsDemonstrationOnly { get; set; } = false;

        // Dietary Options
        public bool IsVegetarian { get; set; } = false;
        public bool IsVegan { get; set; } = false;
        public bool IsGlutenFree { get; set; } = false;
        public bool IsHalal { get; set; } = false;
        public bool IsKosher { get; set; } = false;
        public bool CanAccommodateDietary { get; set; } = true; // Can accommodate special requests

        // Class type
        public bool IsKidsFriendly { get; set; } = false;
        public bool IsCouplesClass { get; set; } = false;
        public bool IsTeamBuilding { get; set; } = false;
        public bool IsPrivateAvailable { get; set; } = true;
        public bool IsOnline { get; set; } = false; // Virtual cooking class

        // Age requirements
        public int? MinAge { get; set; }
        public int? MinAgeWithAdult { get; set; }

        // What to bring
        [MaxLength(500)]
        public string? WhatToBring { get; set; }

        [MaxLength(500)]
        public string? WhatYouLearn { get; set; } // Skills/techniques covered

        // Instructor
        [MaxLength(200)]
        public string? InstructorName { get; set; }

        [MaxLength(1000)]
        public string? InstructorBio { get; set; }

        [MaxLength(500)]
        public string? InstructorImageUrl { get; set; }

        // Schedule
        [MaxLength(500)]
        public string? RecurringSchedule { get; set; } // "Every Saturday 10am" or similar

        // Languages
        [MaxLength(200)]
        public string? AvailableLanguages { get; set; } // Comma-separated: "English, Spanish"

        // External IDs
        [MaxLength(200)]
        public string? ExternalId { get; set; } // Booking platform ID

        [MaxLength(500)]
        [Url]
        public string? BookingUrl { get; set; }

        // Images
        [MaxLength(500)]
        public string? LocalImagePath { get; set; }

        [MaxLength(500)]
        [Url]
        public string? ImageUrl { get; set; }

        // Ratings & Stats
        [Column(TypeName = "decimal(3, 2)")]
        public decimal? AverageRating { get; set; }

        public int TotalReviews { get; set; } = 0;
        public int TotalBookings { get; set; } = 0;

        // Status
        public bool IsActive { get; set; } = true;
        public bool IsNew { get; set; } = false; // Flag for newly added classes
        public bool IsFeatured { get; set; } = false;

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // SEO
        [Required]
        [MaxLength(300)]
        public string Slug { get; set; } = string.Empty;

        // Navigation Properties
        [ForeignKey("SchoolId")]
        public virtual CookingSchool School { get; set; } = null!;

        public virtual ICollection<ClassReview> Reviews { get; set; } = new List<ClassReview>();
        public virtual ICollection<ClassPhoto> Photos { get; set; } = new List<ClassPhoto>();

        // Helper methods
        public string GetDifficultyBadgeClass()
        {
            return DifficultyLevel switch
            {
                DifficultyLevels.Beginner => "bg-success",
                DifficultyLevels.Intermediate => "bg-warning",
                DifficultyLevels.Advanced => "bg-danger",
                DifficultyLevels.AllLevels => "bg-info",
                _ => "bg-secondary"
            };
        }

        public string GetStudentRange()
        {
            if (MinStudents == MaxStudents)
                return $"{MinStudents} students";
            return $"{MinStudents}-{MaxStudents} students";
        }

        public string GetDurationDisplay()
        {
            if (DurationMinutes < 60)
                return $"{DurationMinutes} min";
            var hours = DurationMinutes / 60;
            var minutes = DurationMinutes % 60;
            if (minutes == 0)
                return hours == 1 ? "1 hour" : $"{hours} hours";
            return $"{hours}h {minutes}m";
        }

        public string GetPriceDisplay()
        {
            if (PricePerPerson.HasValue)
                return $"${PricePerPerson:F0}/person";
            if (PriceForPrivateGroup.HasValue)
                return $"${PriceForPrivateGroup:F0}/group";
            return "Contact for pricing";
        }

        public List<string> GetFeatureTags()
        {
            var tags = new List<string>();
            if (HandsOnCooking) tags.Add("Hands-on");
            if (IsDemonstrationOnly) tags.Add("Demo Only");
            if (MealIncluded) tags.Add("Meal Included");
            if (WineParingIncluded) tags.Add("Wine Pairing");
            if (TakeHomeRecipes) tags.Add("Take-home Recipes");
            if (IsVegetarian) tags.Add("Vegetarian");
            if (IsVegan) tags.Add("Vegan");
            if (IsGlutenFree) tags.Add("Gluten-Free");
            if (IsKidsFriendly) tags.Add("Kids Friendly");
            if (IsCouplesClass) tags.Add("Couples");
            if (IsTeamBuilding) tags.Add("Team Building");
            if (IsOnline) tags.Add("Online");
            return tags;
        }

        public List<string> GetDietaryTags()
        {
            var tags = new List<string>();
            if (IsVegetarian) tags.Add("Vegetarian");
            if (IsVegan) tags.Add("Vegan");
            if (IsGlutenFree) tags.Add("Gluten-Free");
            if (IsHalal) tags.Add("Halal");
            if (IsKosher) tags.Add("Kosher");
            if (CanAccommodateDietary) tags.Add("Dietary Accommodations Available");
            return tags;
        }
    }

    /// <summary>
    /// Static class containing cuisine type constants
    /// </summary>
    public static class CuisineTypes
    {
        public const string Italian = "Italian";
        public const string French = "French";
        public const string Asian = "Asian";
        public const string Japanese = "Japanese";
        public const string Chinese = "Chinese";
        public const string Thai = "Thai";
        public const string Vietnamese = "Vietnamese";
        public const string Korean = "Korean";
        public const string Indian = "Indian";
        public const string Mexican = "Mexican";
        public const string Spanish = "Spanish";
        public const string Mediterranean = "Mediterranean";
        public const string MiddleEastern = "Middle Eastern";
        public const string American = "American";
        public const string Southern = "Southern";
        public const string Cajun = "Cajun";
        public const string Baking = "Baking";
        public const string Pastry = "Pastry";
        public const string Desserts = "Desserts";
        public const string Vegetarian = "Vegetarian";
        public const string Vegan = "Vegan";
        public const string BBQ = "BBQ & Grilling";
        public const string Seafood = "Seafood";
        public const string Sushi = "Sushi";
        public const string Pizza = "Pizza";
        public const string Pasta = "Pasta";
        public const string Bread = "Bread Making";
        public const string Chocolate = "Chocolate";
        public const string Cocktails = "Cocktails & Mixology";
        public const string FarmToTable = "Farm to Table";
        public const string PlantBased = "Plant-Based";
        public const string Other = "Other";

        public static List<string> GetAll()
        {
            return new List<string>
            {
                Italian, French, Asian, Japanese, Chinese, Thai, Vietnamese, Korean,
                Indian, Mexican, Spanish, Mediterranean, MiddleEastern, American,
                Southern, Cajun, Baking, Pastry, Desserts, Vegetarian, Vegan,
                BBQ, Seafood, Sushi, Pizza, Pasta, Bread, Chocolate, Cocktails,
                FarmToTable, PlantBased, Other
            };
        }
    }

    /// <summary>
    /// Static class containing difficulty level constants
    /// </summary>
    public static class DifficultyLevels
    {
        public const string Beginner = "Beginner";
        public const string Intermediate = "Intermediate";
        public const string Advanced = "Advanced";
        public const string AllLevels = "All Levels";

        public static List<string> GetAll()
        {
            return new List<string> { Beginner, Intermediate, Advanced, AllLevels };
        }
    }
}
