using CookingClassFinder.Models.Domain;

namespace CookingClassFinder.Services
{
    public class CrawlResult
    {
        public bool Success { get; set; }
        public int SchoolsFound { get; set; }
        public int SchoolsAdded { get; set; }
        public int SchoolsUpdated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IAutoCrawlService
    {
        bool IsRunning { get; }
        Task<CrawlResult> CrawlCityAsync(City city);
        Task<List<City>> GetNextCitiesToCrawlAsync(int count);
        Task SeedCitiesAsync();
        void Stop();
    }
}
