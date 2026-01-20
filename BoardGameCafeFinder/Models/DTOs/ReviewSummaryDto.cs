namespace BoardGameCafeFinder.Models.DTOs
{
    public class ReviewSummaryDto
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorAvatarUrl { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string RelativeDate { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
