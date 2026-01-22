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
        Task<List<BggGameInfo>> GetHotGamesAsync();
        Task<int> SeedBoardGamesFromBggAsync(int count = 500);
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

        /// <summary>
        /// Get multiple game details from BGG in a single API call (batch request)
        /// BGG API supports up to ~20 IDs per request
        /// </summary>
        public async Task<List<BggGameInfo>> GetMultipleGameDetailsAsync(IEnumerable<int> bggIds)
        {
            var games = new List<BggGameInfo>();
            var idList = bggIds.ToList();

            if (idList.Count == 0)
                return games;

            try
            {
                // BGG API: /thing?id=1,2,3,4,5&stats=1
                var idsParam = string.Join(",", idList);
                var url = $"{BggApiBaseUrl}/thing?id={idsParam}&stats=1";

                _logger.LogDebug("Fetching {Count} games from BGG: {Url}", idList.Count, url);

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("BGG API returned {StatusCode} for batch request", response.StatusCode);
                    return games;
                }

                var content = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(content);

                var items = doc.Descendants("item");
                foreach (var item in items)
                {
                    var idAttr = item.Attribute("id")?.Value;
                    if (!int.TryParse(idAttr, out var bggId)) continue;

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

                    games.Add(gameInfo);
                }

                _logger.LogDebug("Successfully fetched {Count} games from BGG batch request", games.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching batch game details from BGG");
            }

            return games;
        }

        /// <summary>
        /// Get hot/trending games from BGG
        /// </summary>
        public async Task<List<BggGameInfo>> GetHotGamesAsync()
        {
            var games = new List<BggGameInfo>();

            try
            {
                var url = $"{BggApiBaseUrl}/hot?type=boardgame";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("BGG API hot games returned {StatusCode}", response.StatusCode);
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
                        ThumbnailUrl = item.Element("thumbnail")?.Attribute("value")?.Value,
                        YearPublished = int.TryParse(item.Element("yearpublished")?.Attribute("value")?.Value, out var year) ? year : null
                    };

                    games.Add(gameInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching BGG hot games");
            }

            return games;
        }

        /// <summary>
        /// Seed board games database from BGG top games
        /// Uses a curated list of popular board game BGG IDs plus hot games
        /// </summary>
        public async Task<int> SeedBoardGamesFromBggAsync(int count = 500)
        {
            int added = 0;

            // Curated list of popular board game BGG IDs (top ranked games on BGG)
            var popularBggIds = new List<int>
            {
                // Top 100 ranked games (approximate)
                174430, // Gloomhaven
                161936, // Pandemic Legacy: Season 1
                224517, // Brass: Birmingham
                167791, // Terraforming Mars
                187645, // Star Wars: Rebellion
                182028, // Through the Ages: A New Story of Civilization
                169786, // Scythe
                173346, // 7 Wonders Duel
                120677, // Terra Mystica
                193738, // Great Western Trail
                28720,  // Brass: Lancashire
                162886, // Spirit Island
                205637, // Arkham Horror: The Card Game
                233078, // Twilight Imperium (Fourth Edition)
                12333,  // Twilight Struggle
                84876,  // The Castles of Burgundy
                31260,  // Agricola
                3076,   // Puerto Rico
                822,    // Carcassonne
                13,     // Catan
                36218,  // Dominion
                68448,  // 7 Wonders
                9209,   // Ticket to Ride
                30549,  // Pandemic
                2651,   // Power Grid
                521,    // Crokinole
                39463,  // Cosmic Encounter
                63888,  // Innovation
                70323,  // King of Tokyo
                72125,  // Eclipse
                102794, // Caverna: The Cave Farmers
                124742, // Android: Netrunner
                110327, // Lords of Waterdeep
                148228, // Splendor
                150376, // Dead of Winter
                164153, // Star Wars: Imperial Assault
                155821, // Inis
                157354, // Five Tribes
                171623, // The Voyages of Marco Polo
                175914, // Food Chain Magnate
                180263, // The 7th Continent
                183394, // Viticulture Essential Edition
                192135, // Too Many Bones
                194594, // Concordia Venus
                199792, // Everdell
                220308, // Gaia Project
                224037, // Architects of the West Kingdom
                230802, // Azul
                233867, // Concordia
                237182, // Root
                244521, // The Quacks of Quedlinburg
                246900, // Eclipse: Second Dawn for the Galaxy
                251247, // Barrage
                256916, // Pax Pamir (Second Edition)
                266192, // Wingspan
                271320, // It's a Wonderful World
                276025, // Maracaibo
                283155, // Calico
                284083, // The Crew: The Quest for Planet Nine
                291457, // Gloomhaven: Jaws of the Lion
                295770, // Paleo
                312484, // Lost Ruins of Arnak
                316554, // Dune: Imperium
                324856, // The Search for Planet X
                329839, // So Clover!
                342942, // Ark Nova
                359871, // Earth
                366013, // Heat: Pedal to the Metal

                // Classic/Popular Games
                2453,   // Blokus
                9217,   // Saboteur
                14996,  // Ticket to Ride: Europe
                21790,  // Thurn and Taxis
                27225,  // Battlestar Galactica
                34635,  // Stone Age
                35497,  // Dixit
                37111,  // Battlestar Galactica
                40692,  // Small World
                41114,  // The Resistance
                43015,  // Forbidden Island
                62219,  // Dominant Species
                66356,  // Quarriors!
                72991,  // Mage Knight Board Game
                96848,  // Mage Wars Arena
                98778,  // Hanabi
                102548, // Coup
                113924, // Codenames
                126163, // Tzolk'in: The Mayan Calendar
                131835, // Forbidden Desert
                144733, // Russian Railroads
                161533, // Lisboa
                164928, // Orléans
                167355, // Nemesis
                170042, // Raiders of the North Sea
                172818, // Clank!
                175640, // Keyflower
                177736, // A Feast for Odin
                178900, // Codenames: Duet
                182874, // Grand Austria Hotel
                184267, // On Mars
                185343, // Anachrony
                191189, // Aeon's End
                199561, // Sagrada
                203993, // Lorenzo il Magnifico
                209010, // Mechs vs. Minions
                215312, // Clank! In! Space!
                218603, // Photosynthesis
                220877, // Coimbra
                225694, // Decrypto
                226255, // The Mind
                228341, // Charterstone
                229853, // Teotihuacan
                230085, // Champion of the Wild
                231398, // Welcome To...
                233371, // Pandemic Legacy: Season 2
                236457, // Architects of the West Kingdom
                239188, // Roll Player
                240980, // Blood Rage
                244522, // Underwater Cities
                245638, // Tiny Towns
                247367, // Res Arcana
                250458, // Clans of Caledonia
                253344, // Taverns of Tiefenthal
                254640, // Just One
                260605, // Wingspan
                262211, // Era: Medieval Age
                262712, // Res Arcana
                264220, // Tainted Grail
                266507, // Cartographers
                269207, // Tapestry
                271896, // Parks
                276498, // On Tour
                281946, // Fort
                285774, // Marvel Champions
                286096, // Tapestry
                291859, // Roll Camera!
                295947, // Cascadia
                300531, // Cubitos
                302260, // Carnegie
                306735, // Crystal Palace
                308765, // Furnace
                312959, // Marvel United
                314491, // Mech. vs. Minions
                317985, // Beyond the Sun
                320725, // Forgotten Waters
                325494, // The Isle of Cats
                327831, // Dorfromantik
                328479, // My City
                329784, // Living Forest
                330592, // Stardew Valley
                332290, // Radlands
                336986, // Flamecraft
                342905, // Verdant
                347048, // Turing Machine
                354568, // Revive
                356123, // Skymines
                359438, // Oathsworn
                362452, // Weather Machine
                366161, // Splendor Duel
                369898, // Voidfall
                372321, // Hegemony
                374173, // Lacrimosa
                381898, // Boonlake

                // Party & Family Games
                111,    // Scrabble
                171,    // Chess
                1406,   // Monopoly
                2083,   // Clue
                2389,   // Backgammon
                2397,   // Boggle
                2453,   // Blokus
                10630,  // Memoir '44
                14105,  // Citadels
                25669,  // Cube Quest
                34219,  // Jamaica
                38453,  // Space Alert
                40628,  // Summoner Wars
                40834,  // Dixit Odyssey
                43443,  // Telestrations
                54043,  // Cosmic Encounter
                66589,  // Risk Legacy
                68425,  // Escape: The Curse of the Temple
                92828,  // Space Cadets
                103343, // Tokaido
                115703, // One Night Ultimate Werewolf
                123260, // Camel Up
                129622, // Love Letter
                133848, // Sheriff of Nottingham
                140620, // Time Stories
                155426, // Broom Service
                156129, // Deception: Murder in Hong Kong
                161970, // Imhotep
                163412, // Patchwork
                176494, // Isle of Skye
                178870, // Santorini
                181304, // Mysterium
                188920, // Captain Sonar
                191004, // Jaipur
                192291, // Sushi Go Party!
                194655, // Santorini
                203417, // Colt Express
                206941, // Kingdomino
                209418, // Ethnos
                215311, // Century: Spice Road
                217372, // Bunny Kingdom
                221965, // Unstable Unicorns
                225883, // Villainous
                234691, // Between Two Castles of Mad King Ludwig
                244711, // Brass: Birmingham
                247030, // Crypt
                250337, // Sagrada
                253883, // Quacks of Quedlinburg
                254928, // Chronicles of Crime
                262543, // Blue Lagoon
                263918, // Cryptid
                266524, // PARKS
                266830, // Carcassonne: Hunters and Gatherers
                271896, // Parks
                279537, // Oriflamme
                280132, // Wavelength
                283435, // Dog Park
                295486, // My City
                300877, // Dune Imperium
                303954, // Scout
                326494, // That's Pretty Clever
            };

            _logger.LogInformation("Starting board game seeding from BGG. Target: {Count} games", count);

            // First, get hot games to include current trending games
            var hotGames = await GetHotGamesAsync();
            foreach (var hot in hotGames)
            {
                if (!popularBggIds.Contains(hot.BggId))
                    popularBggIds.Add(hot.BggId);
            }

            // Get existing BGG IDs from database to skip
            var existingBggIds = await _context.BoardGames
                .Where(g => g.BGGId.HasValue)
                .Select(g => g.BGGId!.Value)
                .ToListAsync();
            var existingBggIdSet = new HashSet<int>(existingBggIds);

            // Filter out already existing games and take requested count
            var idsToFetch = popularBggIds
                .Where(id => !existingBggIdSet.Contains(id))
                .Distinct()
                .Take(count)
                .ToList();

            _logger.LogInformation("Found {Existing} existing games. Will fetch {ToFetch} new games from BGG",
                existingBggIds.Count, idsToFetch.Count);

            if (idsToFetch.Count == 0)
            {
                _logger.LogInformation("No new games to fetch - all games already exist in database");
                return 0;
            }

            // Batch size for BGG API (recommended max ~20 per request)
            const int batchSize = 20;
            var batches = idsToFetch
                .Select((id, index) => new { id, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.id).ToList())
                .ToList();

            _logger.LogInformation("Fetching games in {BatchCount} batches of up to {BatchSize} games each",
                batches.Count, batchSize);

            int batchNumber = 0;
            foreach (var batch in batches)
            {
                batchNumber++;
                try
                {
                    _logger.LogDebug("Processing batch {Batch}/{Total} ({Count} games)",
                        batchNumber, batches.Count, batch.Count);

                    // Fetch multiple games in one API call
                    var gamesInfo = await GetMultipleGameDetailsAsync(batch);

                    foreach (var gameInfo in gamesInfo)
                    {
                        try
                        {
                            var boardGame = new BoardGame
                            {
                                Name = gameInfo.Name,
                                BGGId = gameInfo.BggId,
                                Description = gameInfo.Description?.Length > 4000
                                    ? gameInfo.Description.Substring(0, 4000)
                                    : gameInfo.Description,
                                ImageUrl = gameInfo.ThumbnailUrl ?? gameInfo.ImageUrl,
                                MinPlayers = gameInfo.MinPlayers,
                                MaxPlayers = gameInfo.MaxPlayers,
                                PlaytimeMinutes = gameInfo.PlayingTime,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.BoardGames.Add(boardGame);
                            added++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error creating board game: {Name} (BGG ID: {BggId})",
                                gameInfo.Name, gameInfo.BggId);
                        }
                    }

                    // Save batch to database
                    await _context.SaveChangesAsync();
                    _logger.LogDebug("Saved batch {Batch}: {Count} games added", batchNumber, gamesInfo.Count);

                    // Rate limiting between batches - be nice to BGG API
                    if (batchNumber < batches.Count)
                    {
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing batch {Batch}", batchNumber);
                }
            }

            _logger.LogInformation("Board game seeding completed. Added {Count} games in {Batches} API calls (instead of {Individual} individual calls)",
                added, batches.Count, idsToFetch.Count);
            return added;
        }
    }
}
