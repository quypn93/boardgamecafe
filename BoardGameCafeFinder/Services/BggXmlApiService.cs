using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace BoardGameCafeFinder.Services
{
    public interface IBggXmlApiService
    {
        Task<BggSyncResult> SyncCafeGamesViaApiAsync(int cafeId);
        Task<List<BggGameInfo>> GetUserCollectionAsync(string username);
        Task<BggGameInfo?> GetGameDetailsAsync(int bggId);
        Task<List<BggGameInfo>> SearchGamesAsync(string query);
        Task<List<string>> FindPossibleBggUsernamesAsync(string cafeName);
        Task<BggSyncResult> AutoDiscoverAndSyncAsync(int cafeId);
        Task<List<BggBatchSyncResult>> SyncAllCafesWithAutoDiscoverAsync();
    }

    public class BggGameInfo
    {
        public int BggId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int? YearPublished { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public int? PlayingTime { get; set; }
        public decimal? Rating { get; set; }
        public string? Description { get; set; }
    }

    public class BggXmlApiService : IBggXmlApiService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<BggXmlApiService> _logger;
        private readonly string _apiAccessToken;
        private const string BggApiBaseUrl = "https://boardgamegeek.com/xmlapi2";

        public BggXmlApiService(
            ApplicationDbContext context,
            HttpClient httpClient,
            ILogger<BggXmlApiService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;
            _logger = logger;
            _apiAccessToken = configuration["BoardGameGeek:ApiAccessToken"] ?? string.Empty;

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/xml");
            if (!string.IsNullOrEmpty(_apiAccessToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiAccessToken}");
            }
        }

        public async Task<BggSyncResult> SyncCafeGamesViaApiAsync(int cafeId)
        {
            var result = new BggSyncResult { Success = false };

            var cafe = await _context.Cafes.FindAsync(cafeId);
            if (cafe == null)
            {
                result.Message = "Cafe not found.";
                return result;
            }

            if (string.IsNullOrEmpty(cafe.BggUsername))
            {
                result.Message = "Cafe does not have a BGG username linked.";
                return result;
            }

            try
            {
                _logger.LogInformation("Starting BGG XML API sync for {CafeName} (User: {BggUser})", cafe.Name, cafe.BggUsername);

                var games = await GetUserCollectionAsync(cafe.BggUsername);

                if (games.Count == 0)
                {
                    result.Message = "No games found in the user's collection.";
                    result.Success = true;
                    return result;
                }

                _logger.LogInformation("Found {Count} games in collection for {BggUser}", games.Count, cafe.BggUsername);

                foreach (var gameInfo in games)
                {
                    try
                    {
                        result.GamesProcessed++;

                        var game = await _context.BoardGames.FirstOrDefaultAsync(g => g.BGGId == gameInfo.BggId);
                        if (game == null)
                        {
                            game = new BoardGame
                            {
                                Name = gameInfo.Name,
                                BGGId = gameInfo.BggId,
                                ImageUrl = gameInfo.ThumbnailUrl ?? gameInfo.ImageUrl,
                                Description = gameInfo.Description,
                                MinPlayers = gameInfo.MinPlayers,
                                MaxPlayers = gameInfo.MaxPlayers,
                                PlaytimeMinutes = gameInfo.PlayingTime,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.BoardGames.Add(game);
                            await _context.SaveChangesAsync();
                            result.GamesAdded++;
                        }
                        else
                        {
                            // Update if data is missing
                            bool updated = false;
                            if (string.IsNullOrEmpty(game.ImageUrl) && !string.IsNullOrEmpty(gameInfo.ThumbnailUrl))
                            {
                                game.ImageUrl = gameInfo.ThumbnailUrl ?? gameInfo.ImageUrl;
                                updated = true;
                            }
                            if (game.MinPlayers == null && gameInfo.MinPlayers != null)
                            {
                                game.MinPlayers = gameInfo.MinPlayers;
                                updated = true;
                            }
                            if (game.MaxPlayers == null && gameInfo.MaxPlayers != null)
                            {
                                game.MaxPlayers = gameInfo.MaxPlayers;
                                updated = true;
                            }
                            if (game.PlaytimeMinutes == null && gameInfo.PlayingTime != null)
                            {
                                game.PlaytimeMinutes = gameInfo.PlayingTime;
                                updated = true;
                            }
                            if (updated) result.GamesUpdated++;
                        }

                        // Link game to cafe
                        var cafeGame = await _context.CafeGames.FirstOrDefaultAsync(cg => cg.CafeId == cafeId && cg.GameId == game.GameId);
                        if (cafeGame == null)
                        {
                            cafeGame = new CafeGame
                            {
                                CafeId = cafeId,
                                GameId = game.GameId,
                                IsAvailable = true,
                                LastVerified = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.CafeGames.Add(cafeGame);
                        }
                        else
                        {
                            cafeGame.IsAvailable = true;
                            cafeGame.LastVerified = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing game {GameName} (BGG ID: {BggId})", gameInfo.Name, gameInfo.BggId);
                        result.Errors.Add($"Error processing {gameInfo.Name}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();
                result.Success = true;
                result.Message = $"Successfully synced {result.GamesAdded} new games and updated {result.GamesUpdated}. Total processed: {result.GamesProcessed}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BGG XML API sync failed for cafe {CafeId}", cafeId);
                result.Message = $"API Exception: {ex.Message}";
            }

            return result;
        }

        public async Task<List<BggGameInfo>> GetUserCollectionAsync(string username)
        {
            var games = new List<BggGameInfo>();

            try
            {
                // BGG API might return 202 (queued) - need to retry
                var url = $"{BggApiBaseUrl}/collection?username={Uri.EscapeDataString(username)}&own=1&stats=1";

                int maxRetries = 5;
                int retryDelay = 3000; // 3 seconds

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    var response = await _httpClient.GetAsync(url);

                    if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                    {
                        // 202 - Request queued, wait and retry
                        _logger.LogInformation("BGG API returned 202 (queued), waiting {Delay}ms before retry (attempt {Attempt}/{Max})",
                            retryDelay, attempt + 1, maxRetries);
                        await Task.Delay(retryDelay);
                        retryDelay *= 2; // Exponential backoff
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("BGG API returned {StatusCode} for user {Username}", response.StatusCode, username);
                        return games;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var doc = XDocument.Parse(content);

                    var items = doc.Descendants("item");
                    foreach (var item in items)
                    {
                        var objectId = item.Attribute("objectid")?.Value;
                        if (!int.TryParse(objectId, out var bggId)) continue;

                        var gameInfo = new BggGameInfo
                        {
                            BggId = bggId,
                            Name = item.Element("name")?.Value ?? "Unknown",
                            ThumbnailUrl = item.Element("thumbnail")?.Value,
                            ImageUrl = item.Element("image")?.Value,
                            YearPublished = int.TryParse(item.Element("yearpublished")?.Value, out var year) ? year : null,
                        };

                        // Parse stats if available
                        var stats = item.Element("stats");
                        if (stats != null)
                        {
                            gameInfo.MinPlayers = int.TryParse(stats.Attribute("minplayers")?.Value, out var min) ? min : null;
                            gameInfo.MaxPlayers = int.TryParse(stats.Attribute("maxplayers")?.Value, out var max) ? max : null;
                            gameInfo.PlayingTime = int.TryParse(stats.Attribute("playingtime")?.Value, out var time) ? time : null;

                            var rating = stats.Element("rating")?.Element("average")?.Attribute("value")?.Value;
                            if (decimal.TryParse(rating, out var ratingValue))
                            {
                                gameInfo.Rating = ratingValue;
                            }
                        }

                        games.Add(gameInfo);
                    }

                    break; // Success, exit retry loop
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching BGG collection for user {Username}", username);
            }

            return games;
        }

        public async Task<BggGameInfo?> GetGameDetailsAsync(int bggId)
        {
            try
            {
                var url = $"{BggApiBaseUrl}/thing?id={bggId}&stats=1";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("BGG API returned {StatusCode} for game {BggId}", response.StatusCode, bggId);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(content);

                var item = doc.Descendants("item").FirstOrDefault();
                if (item == null) return null;

                var gameInfo = new BggGameInfo
                {
                    BggId = bggId,
                    Name = item.Elements("name").FirstOrDefault(n => n.Attribute("type")?.Value == "primary")?.Attribute("value")?.Value ?? "Unknown",
                    ThumbnailUrl = item.Element("thumbnail")?.Value,
                    ImageUrl = item.Element("image")?.Value,
                    YearPublished = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out var year) ? year : null,
                    MinPlayers = int.TryParse(item.Element("minplayers")?.Attribute("value")?.Value, out var min) ? min : null,
                    MaxPlayers = int.TryParse(item.Element("maxplayers")?.Attribute("value")?.Value, out var max) ? max : null,
                    PlayingTime = int.TryParse(item.Element("playingtime")?.Attribute("value")?.Value, out var time) ? time : null,
                    Description = item.Element("description")?.Value
                };

                // Get rating
                var rating = item.Element("statistics")?.Element("ratings")?.Element("average")?.Attribute("value")?.Value;
                if (decimal.TryParse(rating, out var ratingValue))
                {
                    gameInfo.Rating = ratingValue;
                }

                return gameInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching BGG game details for {BggId}", bggId);
                return null;
            }
        }

        public async Task<List<BggGameInfo>> SearchGamesAsync(string query)
        {
            var games = new List<BggGameInfo>();

            try
            {
                var url = $"{BggApiBaseUrl}/search?query={Uri.EscapeDataString(query)}&type=boardgame";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("BGG API search returned {StatusCode} for query {Query}", response.StatusCode, query);
                    return games;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(content);

                var items = doc.Descendants("item");
                foreach (var item in items)
                {
                    var id = item.Attribute("id")?.Value;
                    if (!int.TryParse(id, out var bggId)) continue;

                    var gameInfo = new BggGameInfo
                    {
                        BggId = bggId,
                        Name = item.Element("name")?.Attribute("value")?.Value ?? "Unknown",
                        YearPublished = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out var year) ? year : null
                    };

                    games.Add(gameInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching BGG for query {Query}", query);
            }

            return games;
        }

        /// <summary>
        /// Generate possible BGG usernames from cafe name
        /// </summary>
        public async Task<List<string>> FindPossibleBggUsernamesAsync(string cafeName)
        {
            var possibleUsernames = new List<string>();

            // Clean cafe name and generate variations
            var cleanName = cafeName.ToLowerInvariant()
                .Replace("board game", "")
                .Replace("boardgame", "")
                .Replace("cafe", "")
                .Replace("café", "")
                .Replace("bar", "")
                .Replace("lounge", "")
                .Replace("gaming", "")
                .Replace("games", "")
                .Replace("game", "")
                .Replace("tabletop", "")
                .Replace("the ", "")
                .Replace("'s", "")
                .Replace("'", "")
                .Replace("&", "and")
                .Trim();

            // Generate username variations
            var baseNames = new List<string> { cleanName };

            // Add full name without spaces
            var noSpaces = cleanName.Replace(" ", "").Replace("-", "");
            if (!string.IsNullOrEmpty(noSpaces) && noSpaces.Length >= 3)
                baseNames.Add(noSpaces);

            // Add with underscores
            var withUnderscores = cleanName.Replace(" ", "_").Replace("-", "_");
            if (!string.IsNullOrEmpty(withUnderscores) && withUnderscores.Length >= 3)
                baseNames.Add(withUnderscores);

            // Add camelCase
            var words = cleanName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                var camelCase = words[0] + string.Join("", words.Skip(1).Select(w =>
                    char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1) : "")));
                baseNames.Add(camelCase);
            }

            // Add suffixes
            var suffixes = new[] { "", "games", "boardgames", "cafe", "gaming", "bg" };

            foreach (var baseName in baseNames.Distinct())
            {
                foreach (var suffix in suffixes)
                {
                    var username = baseName + suffix;
                    if (!string.IsNullOrEmpty(username) && username.Length >= 3 && !possibleUsernames.Contains(username))
                    {
                        // Check if this user exists and has a collection
                        var hasCollection = await CheckUserHasCollectionAsync(username);
                        if (hasCollection)
                        {
                            possibleUsernames.Add(username);
                            _logger.LogInformation("Found valid BGG user: {Username} for cafe: {CafeName}", username, cafeName);
                        }
                    }
                }
            }

            return possibleUsernames;
        }

        /// <summary>
        /// Check if a BGG user exists and has games in their collection
        /// </summary>
        private async Task<bool> CheckUserHasCollectionAsync(string username)
        {
            try
            {
                var url = $"{BggApiBaseUrl}/collection?username={Uri.EscapeDataString(username)}&own=1&brief=1";

                int maxRetries = 3;
                int retryDelay = 2000;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    var response = await _httpClient.GetAsync(url);

                    if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
                    {
                        await Task.Delay(retryDelay);
                        retryDelay *= 2;
                        continue;
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return false; // User doesn't exist
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var doc = XDocument.Parse(content);

                    // Check if there are any items
                    var itemCount = doc.Descendants("item").Count();
                    return itemCount > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking BGG user {Username}", username);
            }

            return false;
        }

        /// <summary>
        /// Auto-discover BGG username and sync games for a cafe
        /// </summary>
        public async Task<BggSyncResult> AutoDiscoverAndSyncAsync(int cafeId)
        {
            var result = new BggSyncResult { Success = false };

            var cafe = await _context.Cafes.FindAsync(cafeId);
            if (cafe == null)
            {
                result.Message = "Cafe not found.";
                return result;
            }

            // If cafe already has BGG username, just sync
            if (!string.IsNullOrEmpty(cafe.BggUsername))
            {
                return await SyncCafeGamesViaApiAsync(cafeId);
            }

            _logger.LogInformation("Auto-discovering BGG username for cafe: {CafeName}", cafe.Name);

            // Try to find possible usernames
            var possibleUsernames = await FindPossibleBggUsernamesAsync(cafe.Name);

            if (possibleUsernames.Count == 0)
            {
                result.Message = $"Could not find any BGG user matching cafe name: {cafe.Name}";
                return result;
            }

            // Use the first valid username found
            var bggUsername = possibleUsernames.First();
            _logger.LogInformation("Found BGG username '{Username}' for cafe '{CafeName}'", bggUsername, cafe.Name);

            // Save the discovered username
            cafe.BggUsername = bggUsername;
            cafe.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Now sync games
            return await SyncCafeGamesViaApiAsync(cafeId);
        }

        /// <summary>
        /// Sync all cafes, auto-discovering BGG usernames where missing
        /// </summary>
        public async Task<List<BggBatchSyncResult>> SyncAllCafesWithAutoDiscoverAsync()
        {
            var results = new List<BggBatchSyncResult>();

            var cafes = await _context.Cafes.ToListAsync();

            foreach (var cafe in cafes)
            {
                _logger.LogInformation("Processing cafe: {CafeName} (ID: {CafeId})", cafe.Name, cafe.CafeId);

                BggSyncResult syncResult;

                if (!string.IsNullOrEmpty(cafe.BggUsername))
                {
                    // Already has username, just sync
                    syncResult = await SyncCafeGamesViaApiAsync(cafe.CafeId);
                }
                else
                {
                    // Try auto-discover
                    syncResult = await AutoDiscoverAndSyncAsync(cafe.CafeId);
                }

                results.Add(new BggBatchSyncResult
                {
                    CafeId = cafe.CafeId,
                    CafeName = cafe.Name,
                    Result = syncResult
                });

                // Small delay between requests to avoid rate limiting
                await Task.Delay(1000);
            }

            return results;
        }
    }
}
