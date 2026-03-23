namespace CookingClassFinder.Models.DTOs
{
    public class SchoolSearchRequest
    {
        public string? Query { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public string? CuisineType { get; set; } // Filter by cuisine
        public string? DifficultyLevel { get; set; } // Beginner, Intermediate, Advanced
        public int? MinStudents { get; set; }
        public int? MaxStudents { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? IsVegetarian { get; set; }
        public bool? IsVegan { get; set; }
        public bool? IsKidsFriendly { get; set; }
        public bool? IsCouplesClass { get; set; }
        public bool? IsOnline { get; set; }
        public string? SortBy { get; set; } // rating, distance, name, classes_count, price
        public bool SortDescending { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
