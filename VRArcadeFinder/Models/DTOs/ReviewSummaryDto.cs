namespace VRArcadeFinder.Models.DTOs
{
    public class ReviewSummaryDto
    {
        public int ReviewId { get; set; }
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; }

        public string GetTimeAgo()
        {
            var span = DateTime.UtcNow - CreatedAt;

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
