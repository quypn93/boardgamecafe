using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Services
{
    public class BggSyncService : IBggSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBggXmlApiService _bggXmlApiService;
        private readonly ILogger<BggSyncService> _logger;

        public BggSyncService(
            ApplicationDbContext context,
            IBggXmlApiService bggXmlApiService,
            ILogger<BggSyncService> logger)
        {
            _context = context;
            _bggXmlApiService = bggXmlApiService;
            _logger = logger;
        }

        public async Task<BggSyncResult> SyncCafeGamesAsync(int cafeId)
        {
            _logger.LogInformation("Starting BGG sync via XML API for cafe {CafeId}", cafeId);
            return await _bggXmlApiService.SyncCafeGamesViaApiAsync(cafeId);
        }

        public async Task<List<BggBatchSyncResult>> SyncAllCafesAsync()
        {
            var cafes = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.BggUsername))
                .ToListAsync();

            var results = new List<BggBatchSyncResult>();

            foreach (var cafe in cafes)
            {
                _logger.LogInformation("Batch Sync: Starting sync for {CafeName}", cafe.Name);
                var result = await _bggXmlApiService.SyncCafeGamesViaApiAsync(cafe.CafeId);
                results.Add(new BggBatchSyncResult
                {
                    CafeId = cafe.CafeId,
                    CafeName = cafe.Name,
                    Result = result
                });

                // Small delay to avoid BGG rate limiting
                await Task.Delay(1000);
            }

            return results;
        }

        public async Task<List<BggBatchSyncResult>> SyncCafesInCityAsync(string city)
        {
            var cafes = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.BggUsername) && c.City == city)
                .ToListAsync();

            var results = new List<BggBatchSyncResult>();

            foreach (var cafe in cafes)
            {
                _logger.LogInformation("Batch Sync (City: {City}): Starting sync for {CafeName}", city, cafe.Name);
                var result = await _bggXmlApiService.SyncCafeGamesViaApiAsync(cafe.CafeId);
                results.Add(new BggBatchSyncResult
                {
                    CafeId = cafe.CafeId,
                    CafeName = cafe.Name,
                    Result = result
                });

                // Small delay to avoid BGG rate limiting
                await Task.Delay(1000);
            }

            return results;
        }

        public Task<List<BoardGame>> SearchGamesAsync(string query)
        {
            return Task.FromResult(new List<BoardGame>());
        }
    }
}
