using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Services
{
    public interface IBggSyncService
    {
        /// <summary>
        /// Synchronizes the game library of a cafe from BoardGameGeek.
        /// </summary>
        /// <param name="cafeId">The ID of the cafe to sync.</param>
        /// <returns>A summary of the sync operation results.</returns>
        Task<BggSyncResult> SyncCafeGamesAsync(int cafeId);

        /// <summary>
        /// Synchronizes all cafes that have a linked BGG username.
        /// </summary>
        /// <returns>A list of sync results for each cafe.</returns>
        Task<List<BggBatchSyncResult>> SyncAllCafesAsync();

        /// <summary>
        /// Synchronizes all cafes in a specific city that have a linked BGG username.
        /// </summary>
        /// <param name="city">The city name.</param>
        /// <returns>A list of sync results for each cafe.</returns>
        Task<List<BggBatchSyncResult>> SyncCafesInCityAsync(string city);

        /// <summary>
        /// Searches for board games on BoardGameGeek.
        /// </summary>
        /// <param name="query">The search query (name of the game).</param>
        /// <returns>A list of board games found.</returns>
        Task<List<BoardGame>> SearchGamesAsync(string query);
    }

    public class BggSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int GamesProcessed { get; set; }
        public int GamesAdded { get; set; }
        public int GamesUpdated { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class BggBatchSyncResult
    {
        public int CafeId { get; set; }
        public string CafeName { get; set; } = string.Empty;
        public BggSyncResult Result { get; set; } = new();
    }
}
