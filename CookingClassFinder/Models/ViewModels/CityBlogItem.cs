namespace CookingClassFinder.Models.ViewModels
{
    public class CityBlogItem
    {
        public string CityName { get; set; } = string.Empty;
        // Alias for Name
        public string Name { get => CityName; set => CityName = value; }
        public string Country { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public int SchoolCount { get; set; }
        public int ClassCount { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public List<string> TopCuisines { get; set; } = new();
    }
}
