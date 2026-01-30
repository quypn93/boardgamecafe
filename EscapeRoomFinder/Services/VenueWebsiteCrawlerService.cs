using EscapeRoomFinder.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Services
{
    public interface IVenueWebsiteCrawlerService
    {
        Task<List<CrawledRoomData>> CrawlVenueWebsiteForRoomsAsync(string websiteUrl);
    }

    public class VenueWebsiteCrawlerService : IVenueWebsiteCrawlerService
    {
        private readonly ILogger<VenueWebsiteCrawlerService> _logger;
        private readonly ApplicationDbContext _context;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public VenueWebsiteCrawlerService(
            ILogger<VenueWebsiteCrawlerService> logger,
            ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<List<CrawledRoomData>> CrawlVenueWebsiteForRoomsAsync(string websiteUrl)
        {
            var results = new List<CrawledRoomData>();

            if (string.IsNullOrEmpty(websiteUrl))
                return results;

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();

                // Visit main page
                _logger.LogInformation("Visiting venue website: {Url}", websiteUrl);
                await page.GotoAsync(websiteUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });

                // Look for rooms/games page
                var targetUrl = websiteUrl;
                var roomPageUrl = await FindRoomPageUrlAsync(page, websiteUrl);

                if (!string.IsNullOrEmpty(roomPageUrl))
                {
                    _logger.LogInformation("Found rooms page: {Url}", roomPageUrl);
                    try
                    {
                        await page.GotoAsync(roomPageUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.NetworkIdle });
                    }
                    catch
                    {
                        await page.GotoAsync(roomPageUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
                    }
                    targetUrl = roomPageUrl;
                }

                // Extract room data
                results = await ExtractRoomDataAsync(page);

                _logger.LogInformation("Extracted {Count} rooms from {Url}", results.Count, targetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling venue website {Url}", websiteUrl);
            }

            return results;
        }

        private async Task<string?> FindRoomPageUrlAsync(IPage page, string baseUrl)
        {
            var keywords = new[]
            {
                "rooms", "games", "experiences", "adventures", "escape rooms",
                "our rooms", "book", "booking", "play", "missions"
            };

            var links = page.Locator("a");
            var count = await links.CountAsync();

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var href = await links.Nth(i).GetAttributeAsync("href");
                    var text = await links.Nth(i).InnerTextAsync();

                    if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(text))
                    {
                        var combined = (text + " " + href).ToLower();
                        if (keywords.Any(k => combined.Contains(k)))
                        {
                            if (!href.StartsWith("http"))
                            {
                                var uri = new Uri(new Uri(baseUrl), href);
                                return uri.ToString();
                            }
                            return href;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private async Task<List<CrawledRoomData>> ExtractRoomDataAsync(IPage page)
        {
            var rooms = new List<CrawledRoomData>();

            try
            {
                // Strategy 1: Look for structured room cards
                var jsonString = await page.EvaluateAsync<string>(@"() => {
                    const results = [];
                    const clean = (txt) => txt ? txt.trim().replace(/\s+/g, ' ') : '';

                    // Common escape room card selectors
                    const cardSelectors = [
                        '.room-card', '.experience-card', '.game-card',
                        '[class*=""room""]', '[class*=""experience""]',
                        '.card', '.escape-room', 'article'
                    ];

                    for (const selector of cardSelectors) {
                        const cards = document.querySelectorAll(selector);
                        if (cards.length === 0) continue;

                        for (const card of cards) {
                            const room = {};

                            // Extract name from heading
                            const heading = card.querySelector('h1, h2, h3, h4, .title, .name, [class*=""title""]');
                            if (heading) room.name = clean(heading.innerText);

                            // Skip if no name or too generic
                            if (!room.name || room.name.length < 3 || room.name.length > 100) continue;

                            // Extract description
                            const desc = card.querySelector('p, .description, .desc, [class*=""description""]');
                            if (desc) room.description = clean(desc.innerText);

                            // Extract image
                            const img = card.querySelector('img');
                            if (img) {
                                room.imageUrl = img.src || img.getAttribute('data-src');
                                if (room.imageUrl && room.imageUrl.startsWith('//')) {
                                    room.imageUrl = 'https:' + room.imageUrl;
                                }
                            }

                            // Extract theme/genre
                            const themeEl = card.querySelector('[class*=""theme""], [class*=""genre""], [class*=""category""]');
                            if (themeEl) room.theme = clean(themeEl.innerText);

                            // Extract difficulty
                            const text = card.innerText.toLowerCase();
                            if (text.includes('easy') || text.includes('beginner')) room.difficulty = 2;
                            else if (text.includes('medium') || text.includes('moderate')) room.difficulty = 3;
                            else if (text.includes('hard') || text.includes('difficult')) room.difficulty = 4;
                            else if (text.includes('expert') || text.includes('extreme')) room.difficulty = 5;

                            // Extract player count
                            const playerMatch = text.match(/(\d+)\s*[-–to]\s*(\d+)\s*(?:player|people|person)/i);
                            if (playerMatch) {
                                room.minPlayers = parseInt(playerMatch[1]);
                                room.maxPlayers = parseInt(playerMatch[2]);
                            }

                            // Extract duration
                            const durationMatch = text.match(/(\d+)\s*(?:min|minute)/i);
                            if (durationMatch) {
                                room.durationMinutes = parseInt(durationMatch[1]);
                            }

                            // Extract price
                            const priceMatch = text.match(/\$\s*(\d+(?:\.\d{2})?)/);
                            if (priceMatch) {
                                room.price = parseFloat(priceMatch[1]);
                            }

                            results.push(room);
                        }

                        if (results.length > 0) break;
                    }

                    return JSON.stringify(results);
                }");

                if (!string.IsNullOrEmpty(jsonString))
                {
                    var crawledRooms = JsonSerializer.Deserialize<List<CrawledRoomDataJson>>(jsonString, _jsonOptions);

                    if (crawledRooms != null)
                    {
                        foreach (var item in crawledRooms)
                        {
                            if (string.IsNullOrEmpty(item.Name)) continue;

                            // Validate it looks like an escape room name
                            if (!IsLikelyEscapeRoomName(item.Name)) continue;

                            rooms.Add(new CrawledRoomData
                            {
                                Name = item.Name,
                                Description = item.Description,
                                Theme = item.Theme ?? InferTheme(item.Name, item.Description),
                                Difficulty = item.Difficulty ?? 3,
                                MinPlayers = item.MinPlayers ?? 2,
                                MaxPlayers = item.MaxPlayers ?? 6,
                                DurationMinutes = item.DurationMinutes ?? 60,
                                Price = item.Price,
                                ImageUrl = item.ImageUrl
                            });
                        }
                    }
                }

                // Strategy 2: If no structured data found, try to extract from generic content
                if (rooms.Count == 0)
                {
                    rooms = await ExtractFromGenericContentAsync(page);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting room data");
            }

            return rooms.DistinctBy(r => r.Name).ToList();
        }

        private async Task<List<CrawledRoomData>> ExtractFromGenericContentAsync(IPage page)
        {
            var rooms = new List<CrawledRoomData>();

            try
            {
                // Look for headings that might be room names
                var headings = await page.Locator("h1, h2, h3, h4").AllAsync();

                foreach (var heading in headings)
                {
                    try
                    {
                        var text = await heading.TextContentAsync();
                        if (string.IsNullOrEmpty(text)) continue;

                        text = text.Trim();

                        if (IsLikelyEscapeRoomName(text))
                        {
                            var room = new CrawledRoomData
                            {
                                Name = text,
                                Theme = InferTheme(text, null),
                                Difficulty = 3,
                                MinPlayers = 2,
                                MaxPlayers = 6,
                                DurationMinutes = 60
                            };

                            // Try to get description from next sibling
                            var parent = heading.Locator("..").First;
                            var desc = parent.Locator("p").First;
                            if (await desc.IsVisibleAsync())
                            {
                                room.Description = await desc.TextContentAsync();
                            }

                            // Try to get image
                            var img = parent.Locator("img").First;
                            if (await img.IsVisibleAsync())
                            {
                                room.ImageUrl = await img.GetAttributeAsync("src");
                            }

                            rooms.Add(room);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting from generic content");
            }

            return rooms;
        }

        private bool IsLikelyEscapeRoomName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            var trimmed = name.Trim();

            // Length checks
            if (trimmed.Length < 3 || trimmed.Length > 100) return false;

            // Exclude common website elements
            var excludeExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "home", "about", "contact", "book now", "book", "booking",
                "faq", "reviews", "gallery", "photos", "location", "directions",
                "gift cards", "gift certificates", "team building", "corporate events",
                "birthday parties", "private events", "group events",
                "login", "sign in", "sign up", "register", "cart", "checkout",
                "terms", "privacy", "policy", "careers", "jobs"
            };

            if (excludeExact.Contains(trimmed)) return false;

            // Check for escape room related keywords
            var positiveKeywords = new[]
            {
                "escape", "room", "mission", "adventure", "mystery", "puzzle",
                "quest", "vault", "heist", "prison", "asylum", "haunted",
                "zombie", "detective", "crime", "secret", "spy", "agent"
            };

            var lowerName = trimmed.ToLower();
            return positiveKeywords.Any(k => lowerName.Contains(k)) ||
                   !lowerName.Any(c => char.IsDigit(c)) && trimmed.Split(' ').Length <= 6;
        }

        private string InferTheme(string name, string? description)
        {
            var text = (name + " " + (description ?? "")).ToLower();

            var themeKeywords = new Dictionary<string, string[]>
            {
                { "Horror", new[] { "horror", "zombie", "haunted", "asylum", "possessed", "demon", "evil", "dark", "nightmare", "terror" } },
                { "Mystery", new[] { "mystery", "detective", "murder", "crime", "investigation", "sherlock", "clue" } },
                { "Adventure", new[] { "adventure", "treasure", "expedition", "explorer", "jungle", "temple", "tomb" } },
                { "Sci-Fi", new[] { "space", "alien", "sci-fi", "future", "robot", "cyber", "laboratory", "experiment" } },
                { "Fantasy", new[] { "magic", "wizard", "dragon", "medieval", "castle", "enchanted", "fairy" } },
                { "Heist", new[] { "heist", "bank", "vault", "robbery", "steal", "thief", "diamond" } },
                { "Prison", new[] { "prison", "jail", "escape", "cell", "warden", "inmate" } },
                { "Spy", new[] { "spy", "agent", "secret", "mission", "classified", "intelligence" } },
                { "Historical", new[] { "historical", "ancient", "egyptian", "roman", "pirate", "war" } }
            };

            foreach (var (theme, keywords) in themeKeywords)
            {
                if (keywords.Any(k => text.Contains(k)))
                {
                    return theme;
                }
            }

            return "Mystery"; // Default theme
        }

        // Internal class for JSON deserialization
        private class CrawledRoomDataJson
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? Theme { get; set; }
            public int? Difficulty { get; set; }
            public int? MinPlayers { get; set; }
            public int? MaxPlayers { get; set; }
            public int? DurationMinutes { get; set; }
            public decimal? Price { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}
