namespace CookingClassFinder.Models.DTOs
{
    // Single school result for API
    public class SchoolSearchResultDto
    {
        public int SchoolId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? LocalImagePath { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalClasses { get; set; }
        public bool IsPremium { get; set; }
        public double DistanceKm { get; set; }
    }

    // Paginated search result (internal use)
    public class SchoolSearchPagedResult
    {
        public List<SchoolListItemDto> Schools { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage => Page < TotalPages;
        public bool HasPreviousPage => Page > 1;
    }

    public class SchoolListItemDto
    {
        public int SchoolId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string? State { get; set; }
        public string Country { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? LocalImagePath { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalClasses { get; set; }
        public bool IsVerified { get; set; }
        public bool IsPremium { get; set; }
        public double DistanceKm { get; set; }

        // Class summary
        public List<ClassSummaryDto> Classes { get; set; } = new();
        public List<string> CuisineSpecialties { get; set; } = new();
        public decimal? LowestPrice { get; set; }
        public decimal? HighestPrice { get; set; }
    }

    public class ClassSummaryDto
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string CuisineType { get; set; } = string.Empty;
        public string DifficultyLevel { get; set; } = string.Empty;
        public int MinStudents { get; set; }
        public int MaxStudents { get; set; }
        public int DurationMinutes { get; set; }
        public decimal? PricePerPerson { get; set; }
        public decimal? AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string? LocalImagePath { get; set; }
        public bool IsVegetarian { get; set; }
        public bool IsVegan { get; set; }
        public bool MealIncluded { get; set; }
    }
}
