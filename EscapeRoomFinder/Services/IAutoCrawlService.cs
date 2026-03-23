namespace EscapeRoomFinder.Services
{
    public interface IAutoCrawlService
    {
        bool IsRunning { get; }
        string? CurrentCity { get; }
        Task RunCrawlBatchAsync(CancellationToken cancellationToken = default);
        Task<(int venuesFound, int venuesAdded, int venuesUpdated, string? error)> CrawlCityAsync(int cityId, CancellationToken cancellationToken = default);
        Task SeedCitiesAsync();
        void Stop();
    }
}
