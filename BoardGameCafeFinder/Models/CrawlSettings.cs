namespace BoardGameCafeFinder.Models
{
    public class CrawlSettings
    {
        public bool EnableAutoCrawl { get; set; } = true;
        public int CitiesPerBatch { get; set; } = 3;
        public int RetryDelayHours { get; set; } = 2;
        public int DelayBetweenCitiesSeconds { get; set; } = 30;
        public int MaxResultsPerCity { get; set; } = 15;
        public int CheckIntervalMinutes { get; set; } = 30;
        public string[] SearchQueries { get; set; } = new[]
        {
            "board game cafe",
            "tabletop game cafe",
            "board game bar",
            "gaming cafe board games",
            "tabletop gaming lounge"
        };
    }
}
