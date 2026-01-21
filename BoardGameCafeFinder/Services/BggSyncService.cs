using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace BoardGameCafeFinder.Services
{
    public class BggSyncService : IBggSyncService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<BggSyncService> _logger;

        public BggSyncService(
            ApplicationDbContext context,
            HttpClient httpClient,
            ILogger<BggSyncService> logger)
        {
            _context = context;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<BggSyncResult> SyncCafeGamesAsync(int cafeId)
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
                _logger.LogInformation("Starting BGG Gallery Scraping sync for {CafeName} (User: {BggUser})", cafe.Name, cafe.BggUsername);

                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                });
                var page = await context.NewPageAsync();

                int currentPage = 1;
                bool hasMorePages = true;

                while (hasMorePages)
                {
                    // Using gallery=large for better thumbnail access
                    var url = $"https://boardgamegeek.com/collection/user/{Uri.EscapeDataString(cafe.BggUsername)}?own=1&gallery=large&page={currentPage}";
                    _logger.LogInformation("Scraping BGG gallery page {Page}: {Url}", currentPage, url);

                    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });

                    // Check for items
                    var gameItems = await page.Locator("div[style*='float:left'][style*='text-align:center']").AllAsync();
                    
                    if (gameItems.Count == 0)
                    {
                        // Fallback: Check if it's using a table-based gallery or list
                        gameItems = await page.Locator("table.collection_table tr[id^='row_']").AllAsync();
                        if (gameItems.Count == 0)
                        {
                            _logger.LogInformation("No more games found on page {Page}. Ending sync.", currentPage);
                            break;
                        }
                    }

                    _logger.LogInformation("Found {Count} games on page {Page}.", gameItems.Count, currentPage);

                    foreach (var item in gameItems)
                    {
                        try
                        {
                            // Selector for title link (avoiding the one with the image)
                            var titleLink = item.Locator("a[href*='/boardgame/']:not(:has(img))").First;
                            if (await titleLink.CountAsync() == 0) 
                            {
                                // Try second link in the container which is usually the title
                                titleLink = item.Locator("a[href*='/boardgame/']").Nth(1);
                            }
                            
                            if (await titleLink.CountAsync() == 0) continue;

                            var name = await titleLink.InnerTextAsync();
                            var href = await titleLink.GetAttributeAsync("href");
                            
                            // Extract BGG ID from href
                            var match = Regex.Match(href ?? "", @"/boardgame/(\d+)/");
                            if (!match.Success) match = Regex.Match(href ?? "", @"/boardgameexpansion/(\d+)/");
                            
                            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var bggId)) continue;

                            var imgElement = item.Locator("img");
                            var thumbnailUrl = await imgElement.CountAsync() > 0 ? await imgElement.GetAttributeAsync("src") : null;

                            // Handle lazy loading if necessary (BGG sometimes uses data-src)
                            if (string.IsNullOrEmpty(thumbnailUrl) || thumbnailUrl.Contains("clear.tst"))
                            {
                                thumbnailUrl = await imgElement.GetAttributeAsync("data-src") ?? thumbnailUrl;
                            }

                            result.GamesProcessed++;

                            // DB Update
                            var game = await _context.BoardGames.FirstOrDefaultAsync(g => g.BGGId == bggId);
                            if (game == null)
                            {
                                game = new BoardGame
                                {
                                    Name = name,
                                    BGGId = bggId,
                                    ImageUrl = thumbnailUrl,
                                    CreatedAt = DateTime.UtcNow
                                };
                                _context.BoardGames.Add(game);
                                await _context.SaveChangesAsync();
                                result.GamesAdded++;
                            }
                            else if (string.IsNullOrEmpty(game.ImageUrl) && !string.IsNullOrEmpty(thumbnailUrl))
                            {
                                // Update image if it was missing
                                game.ImageUrl = thumbnailUrl;
                                result.GamesUpdated++;
                            }

                            var cafeGame = _context.CafeGames.Local.FirstOrDefault(cg => cg.CafeId == cafeId && cg.GameId == game.GameId) 
                                ?? await _context.CafeGames.FirstOrDefaultAsync(cg => cg.CafeId == cafeId && cg.GameId == game.GameId);

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
                            _logger.LogWarning(ex, "Error processing a game in BGG scraper");
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Check for Next page
                    var nextButton = page.Locator("a:has-text('Next »')").First;
                    if (await nextButton.CountAsync() > 0 && await nextButton.IsVisibleAsync())
                    {
                        currentPage++;
                    }
                    else
                    {
                        hasMorePages = false;
                    }
                }

                result.Success = true;
                result.Message = $"Successfully synced {result.GamesAdded} new games and updated {result.GamesUpdated}. Total processed: {result.GamesProcessed}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BGG scraping sync failed");
                result.Message = $"Scraping Exception: {ex.Message}";
            }

            return result;
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
                var result = await SyncCafeGamesAsync(cafe.CafeId);
                results.Add(new BggBatchSyncResult
                {
                    CafeId = cafe.CafeId,
                    CafeName = cafe.Name,
                    Result = result
                });
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
                var result = await SyncCafeGamesAsync(cafe.CafeId);
                results.Add(new BggBatchSyncResult
                {
                    CafeId = cafe.CafeId,
                    CafeName = cafe.Name,
                    Result = result
                });
            }

            return results;
        }

        public Task<List<BoardGame>> SearchGamesAsync(string query)
        {
            return Task.FromResult(new List<BoardGame>());
        }
    }
}
