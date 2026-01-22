using Microsoft.Playwright;
using BoardGameCafeFinder.Models.Domain;
using System.Text.RegularExpressions;

namespace BoardGameCafeFinder.Services
{
    public interface IGoogleMapsCrawlerService
    {
        Task<List<CrawledCafeData>> CrawlBoardGameCafesAsync(string location, int maxResults = 20);
        Task<List<CrawledCafeData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10);
        Task SaveCrawledReviewsAsync(int cafeId, List<CrawledReviewData> crawledReviews);
        List<BoardGameCafeFinder.Models.Domain.Review> ConvertToReviews(int cafeId, List<CrawledReviewData> crawledReviews, int? systemUserId = null);
    }

    public class CrawledReviewData
    {
        public string Author { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime? ReviewDate { get; set; }
        public int? HelpfulCount { get; set; }
    }

    public class CrawledCafeData
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public double? Rating { get; set; }
        public int? ReviewCount { get; set; }
        public string? PriceLevel { get; set; }
        public string? OpeningHours { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? GoogleMapsUrl { get; set; }
        public string? GooglePlaceId { get; set; }
        public string? ImageUrl { get; set; }
        public string? LocalImagePath { get; set; }
        public List<string> Categories { get; set; } = new();
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public List<CrawledReviewData> Reviews { get; set; } = new();
        public List<string> PhotoUrls { get; set; } = new();
        public List<string> PhotoLocalPaths { get; set; } = new();
        public Dictionary<string, List<string>> Attributes { get; set; } = new();
        public string? BggUsername { get; set; }
        public List<CrawledGameData> FoundGames { get; set; } = new();
    }

    public class GoogleMapsCrawlerService : IGoogleMapsCrawlerService
    {
        private readonly ILogger<GoogleMapsCrawlerService> _logger;
        private readonly IImageStorageService _imageStorageService;
        private readonly ICafeWebsiteCrawlerService _cafeWebsiteCrawlerService;
        private readonly IBggXmlApiService _bggXmlApiService;

        public GoogleMapsCrawlerService(
            ILogger<GoogleMapsCrawlerService> logger,
            IImageStorageService imageStorageService,
            ICafeWebsiteCrawlerService cafeWebsiteCrawlerService,
            IBggXmlApiService bggXmlApiService)
        {
            _logger = logger;
            _imageStorageService = imageStorageService;
            _cafeWebsiteCrawlerService = cafeWebsiteCrawlerService;
            _bggXmlApiService = bggXmlApiService;
        }

        public async Task<List<CrawledCafeData>> CrawlBoardGameCafesAsync(string location, int maxResults = 20)
        {
            var results = new List<CrawledCafeData>();

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    Locale = "en-US"
                });

                var page = await context.NewPageAsync();

                // Search for board game cafes
                var searchQuery = $"board game cafe {location}";
                var searchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(searchQuery)}";

                _logger.LogInformation("Navigating to: {Url}", searchUrl);
                await page.GotoAsync(searchUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

                // Wait for results to load
                await page.WaitForTimeoutAsync(3000);

                // Check if we already landed on a details page (single result)
                // Single results don't have a 'feed' but have the name header visible
                var nameElement = page.Locator("h1.DUwDvf").First;
                var feedLocator = page.Locator("div[role='feed']");

                if (await nameElement.CountAsync() > 0 && await nameElement.IsVisibleAsync() && await feedLocator.CountAsync() == 0)
                {
                    _logger.LogInformation("Single result detected (direct details page). Extracting directly.");
                    var cafeData = await ExtractCafeDataAsync(page);
                    if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                    {
                        results.Add(cafeData);
                    }
                    await browser.CloseAsync();
                    return results;
                }

                // Get all result items initially to estimate count, but don't rely on it for stale elements
                var itemsSelector = "div[role='feed'] > div > div[jsaction]";
                
                // We will loop based on maxResults, trying to find/scroll to the next item
                for (int i = 0; i < maxResults; i++)
                {
                    try
                    {
                        // 1. Ensure the item at index 'i' is loaded in the DOM
                        var itemLocator = page.Locator(itemsSelector).Nth(i);
                        var retries = 0;
                        while (await itemLocator.CountAsync() == 0 && retries < 5)
                        {
                            // Scroll feed to load more
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                            await page.WaitForTimeoutAsync(2000);
                            retries++;
                        }

                        if (await itemLocator.CountAsync() == 0)
                        {
                            _logger.LogInformation("No more items found after scrolling. Stopping at index {Index}", i);
                            break;
                        }

                        // 2. Scroll the specific item into view (critical for virtual lists)
                        // Sometimes simple ScrollIntoViewIfNeededAsync isn't enough if the container is weird
                         try {
                            await itemLocator.ScrollIntoViewIfNeededAsync();
                         } catch {
                            // Fallback scroll on feed
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop += 500");
                         }
                        
                        // 3. Click and Extract
                        await itemLocator.ClickAsync();
                        await page.WaitForTimeoutAsync(1500);

                        var cafeData = await ExtractCafeDataAsync(page);

                        if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                        {
                            results.Add(cafeData);
                            _logger.LogInformation("Extracted ({Index}): {Name}", i + 1, cafeData.Name);
                        }

                        // 4. Go back carefully
                        var backToResults = page.Locator("button[aria-label='Back']").First;
                        if (await backToResults.CountAsync() > 0 && await backToResults.IsVisibleAsync())
                        {
                            await backToResults.ClickAsync();
                        }
                        else
                        {
                            await page.GoBackAsync();
                        }

                        await page.WaitForTimeoutAsync(1500);
                        
                        // 5. Re-focus/Re-align list state
                         var resultsPanel = page.Locator("div[role='feed']");
                         if (await resultsPanel.CountAsync() > 0)
                         {
                             // Sometimes clicking the panel helps regain focus for hotkeys/scrolling
                             await resultsPanel.ClickAsync(new LocatorClickOptions { Force = true });
                         }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to extract item {Index}: {Message}", i, ex.Message);
                        
                        // Recover navigation if stuck
                        try 
                        {
                             var backToResults = page.Locator("button[aria-label='Back']").First;
                             if (await backToResults.CountAsync() > 0 && await backToResults.IsVisibleAsync())
                             {
                                 await backToResults.ClickAsync();
                                 await page.WaitForTimeoutAsync(2000);
                             }
                        }
                        catch {}
                    }
                }

                await browser.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling Google Maps");
            }

            return results;
        }

        public async Task<List<CrawledCafeData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10)
        {
            var allResults = new List<CrawledCafeData>();
            var seenPlaceIds = new HashSet<string>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Reuse single browser instance across all queries to avoid repeated startup overhead
            // and reduce chance of being blocked by Google
            IPlaywright? playwright = null;
            IBrowser? browser = null;

            try
            {
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Timeout = 60000 // 60 second timeout for browser launch
                });

                _logger.LogInformation("Browser started for multi-query crawl: {Location} with {QueryCount} queries",
                    location, queries.Length);

                foreach (var query in queries)
                {
                    try
                    {
                        _logger.LogInformation("Crawling with query: '{Query}' for location: {Location}", query, location);

                        // Use shared browser instance
                        var results = await CrawlWithCustomQueryUsingBrowserAsync(browser, location, query, maxResultsPerQuery);

                        foreach (var cafe in results)
                        {
                            // Deduplicate by GooglePlaceId or Name
                            var placeId = cafe.GooglePlaceId ?? "";
                            var name = cafe.Name ?? "";

                            if (!string.IsNullOrEmpty(placeId) && seenPlaceIds.Contains(placeId))
                            {
                                _logger.LogDebug("Skipping duplicate cafe by PlaceId: {Name}", name);
                                continue;
                            }

                            if (!string.IsNullOrEmpty(name) && seenNames.Contains(name))
                            {
                                _logger.LogDebug("Skipping duplicate cafe by Name: {Name}", name);
                                continue;
                            }

                            if (!string.IsNullOrEmpty(placeId))
                                seenPlaceIds.Add(placeId);
                            if (!string.IsNullOrEmpty(name))
                                seenNames.Add(name);

                            allResults.Add(cafe);
                        }

                        _logger.LogInformation("Query '{Query}' found {Count} unique cafes (total now: {Total})",
                            query, results.Count, allResults.Count);

                        // Delay between queries to avoid rate limiting
                        await Task.Delay(2000);
                    }
                    catch (TimeoutException tex)
                    {
                        _logger.LogWarning("Timeout with query '{Query}' for {Location}: {Message}", query, location, tex.Message);
                        // Continue to next query instead of failing completely
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error with query '{Query}' for {Location}", query, location);
                        // Continue to next query
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in multi-query crawl for {Location}", location);
            }
            finally
            {
                // Cleanup
                if (browser != null)
                {
                    try { await browser.CloseAsync(); } catch { }
                }
                playwright?.Dispose();
            }

            _logger.LogInformation("Total unique cafes found for {Location}: {Count}", location, allResults.Count);
            return allResults;
        }

        /// <summary>
        /// Crawl using an existing browser instance (for multi-query efficiency)
        /// </summary>
        private async Task<List<CrawledCafeData>> CrawlWithCustomQueryUsingBrowserAsync(IBrowser browser, string location, string searchTerm, int maxResults)
        {
            var results = new List<CrawledCafeData>();
            IBrowserContext? context = null;
            IPage? page = null;

            try
            {
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    Locale = "en-US"
                });

                // Set default timeout for all operations
                context.SetDefaultTimeout(30000); // 30 seconds

                page = await context.NewPageAsync();

                var searchQuery = $"{searchTerm} {location}";
                var searchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(searchQuery)}";

                _logger.LogDebug("Navigating to: {Url}", searchUrl);
                await page.GotoAsync(searchUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 45000 // 45 second timeout for page load
                });

                await page.WaitForTimeoutAsync(3000);

                // Check if single result
                var nameElement = page.Locator("h1.DUwDvf").First;
                var feedLocator = page.Locator("div[role='feed']");

                if (await nameElement.CountAsync() > 0 && await nameElement.IsVisibleAsync() && await feedLocator.CountAsync() == 0)
                {
                    var cafeData = await ExtractCafeDataAsync(page);
                    if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                    {
                        results.Add(cafeData);
                    }
                    return results;
                }

                // Multiple results - iterate through list
                var itemsSelector = "div[role='feed'] > div > div[jsaction]";

                for (int i = 0; i < maxResults; i++)
                {
                    try
                    {
                        var itemLocator = page.Locator(itemsSelector).Nth(i);
                        var retries = 0;
                        while (await itemLocator.CountAsync() == 0 && retries < 3)
                        {
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                            await page.WaitForTimeoutAsync(1500);
                            retries++;
                        }

                        if (await itemLocator.CountAsync() == 0)
                            break;

                        try
                        {
                            await itemLocator.ScrollIntoViewIfNeededAsync();
                        }
                        catch
                        {
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop += 500");
                        }

                        await itemLocator.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
                        await page.WaitForTimeoutAsync(1500);

                        var cafeData = await ExtractCafeDataAsync(page);

                        if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                        {
                            results.Add(cafeData);
                        }

                        var backToResults = page.Locator("button[aria-label='Back']").First;
                        if (await backToResults.CountAsync() > 0 && await backToResults.IsVisibleAsync())
                        {
                            await backToResults.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                        }
                        else
                        {
                            await page.GoBackAsync(new PageGoBackOptions { Timeout = 10000 });
                        }

                        await page.WaitForTimeoutAsync(1000);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogDebug("Timeout extracting item {Index} for query '{Query}'", i, searchTerm);
                        // Try to recover
                        try
                        {
                            var backToResults = page.Locator("button[aria-label='Back']").First;
                            if (await backToResults.CountAsync() > 0)
                            {
                                await backToResults.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                                await page.WaitForTimeoutAsync(1000);
                            }
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Error extracting item {Index}: {Message}", i, ex.Message);
                        try
                        {
                            var backToResults = page.Locator("button[aria-label='Back']").First;
                            if (await backToResults.CountAsync() > 0)
                            {
                                await backToResults.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
                                await page.WaitForTimeoutAsync(1000);
                            }
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                // Always close context (but not browser - it's shared)
                if (context != null)
                {
                    try { await context.CloseAsync(); } catch { }
                }
            }

            return results;
        }

        private async Task<List<CrawledCafeData>> CrawlWithCustomQueryAsync(string location, string searchTerm, int maxResults)
        {
            var results = new List<CrawledCafeData>();

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                });

                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                    Locale = "en-US"
                });

                var page = await context.NewPageAsync();

                var searchQuery = $"{searchTerm} {location}";
                var searchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(searchQuery)}";

                _logger.LogInformation("Navigating to: {Url}", searchUrl);
                await page.GotoAsync(searchUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000
                });

                await page.WaitForTimeoutAsync(5000);

                // Check if single result
                var nameElement = page.Locator("h1.DUwDvf").First;
                var feedLocator = page.Locator("div[role='feed']");

                if (await nameElement.CountAsync() > 0 && await nameElement.IsVisibleAsync() && await feedLocator.CountAsync() == 0)
                {
                    var cafeData = await ExtractCafeDataAsync(page);
                    if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                    {
                        results.Add(cafeData);
                    }
                    await browser.CloseAsync();
                    return results;
                }

                // Multiple results - iterate through list
                var itemsSelector = "div[role='feed'] > div > div[jsaction]";

                for (int i = 0; i < maxResults; i++)
                {
                    try
                    {
                        var itemLocator = page.Locator(itemsSelector).Nth(i);
                        var retries = 0;
                        while (await itemLocator.CountAsync() == 0 && retries < 3)
                        {
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                            await page.WaitForTimeoutAsync(2000);
                            retries++;
                        }

                        if (await itemLocator.CountAsync() == 0)
                            break;

                        try
                        {
                            await itemLocator.ScrollIntoViewIfNeededAsync();
                        }
                        catch
                        {
                            var feed = page.Locator("div[role='feed']");
                            await feed.EvaluateAsync("el => el.scrollTop += 500");
                        }

                        await itemLocator.ClickAsync();
                        await page.WaitForTimeoutAsync(2000);

                        var cafeData = await ExtractCafeDataAsync(page);

                        if (cafeData != null && !string.IsNullOrEmpty(cafeData.Name))
                        {
                            results.Add(cafeData);
                        }

                        var backToResults = page.Locator("button[aria-label='Back']").First;
                        if (await backToResults.CountAsync() > 0 && await backToResults.IsVisibleAsync())
                        {
                            await backToResults.ClickAsync();
                        }
                        else
                        {
                            await page.GoBackAsync();
                        }

                        await page.WaitForTimeoutAsync(1500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to extract item {Index}: {Message}", i, ex.Message);
                        try
                        {
                            var backToResults = page.Locator("button[aria-label='Back']").First;
                            if (await backToResults.CountAsync() > 0 && await backToResults.IsVisibleAsync())
                            {
                                await backToResults.ClickAsync();
                                await page.WaitForTimeoutAsync(1500);
                            }
                        }
                        catch { }
                    }
                }

                await browser.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling with query '{Query}' for {Location}", searchTerm, location);
            }

            return results;
        }

        private async Task<CrawledCafeData?> ExtractCafeDataAsync(IPage page)
        {
            try
            {
                // Check if permanently closed - skip these
                var closedIndicator = page.Locator("span:has-text('Permanently closed')").First;
                if (await closedIndicator.CountAsync() > 0)
                {
                    _logger.LogInformation("Skipping permanently closed place");
                    return null;
                }

                var cafe = new CrawledCafeData();

                // Extract name from URL (most reliable)
                var currentUrl = page.Url;
                var nameMatch = Regex.Match(currentUrl, @"/place/([^/]+)/@");
                if (nameMatch.Success)
                {
                    cafe.Name = Uri.UnescapeDataString(nameMatch.Groups[1].Value.Replace("+", " "));
                }
                else
                {
                    // Fallback: try h1
                    var nameElement = page.Locator("h1.DUwDvf").First;
                    if (await nameElement.CountAsync() > 0)
                    {
                        cafe.Name = await nameElement.InnerTextAsync();
                    }
                }

                // Extract rating and review count using robust JS evaluation
                var metroData = await page.EvaluateAsync<dynamic>(@"() => {
                    const findValue = (selectors, keywords, regex) => {
                        const elements = Array.from(document.querySelectorAll(selectors));
                        for (const el of elements) {
                            const textSource = (el.ariaLabel || el.innerText || '').toLowerCase();
                            if (keywords.some(k => textSource.includes(k))) {
                                const match = textSource.match(regex);
                                if (match) return match[1];
                            }
                        }
                        return null;
                    };

                    const rating = findValue('div[role=""img""]', ['star', 'sao'], /([\d,.]+)/);
                    const reviews = findValue('button, span, a', ['review', 'đánh giá'], /([\d,.]+)/);

                    return { rating, reviews };
                }");

                if (metroData != null)
                {
                    if (metroData.rating != null)
                    {
                        string ratingStr = metroData.rating.ToString();
                        ratingStr = ratingStr.Replace(",", ".");
                        if (double.TryParse(ratingStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var r))
                            cafe.Rating = r;
                    }

                    if (metroData.reviews != null)
                    {
                        string reviewsStr = metroData.reviews.ToString();
                        reviewsStr = reviewsStr.Replace(",", "").Replace(".", "");
                        if (int.TryParse(reviewsStr, out var rv))
                            cafe.ReviewCount = rv;
                    }
                }

                // Extract address
                var addressElement = page.Locator("button[data-item-id='address']").First;
                if (await addressElement.CountAsync() > 0)
                {
                    cafe.Address = await addressElement.InnerTextAsync();
                }
                else
                {
                    // Alternative selector
                    var addressAlt = page.Locator("[data-item-id='address'] div.fontBodyMedium").First;
                    if (await addressAlt.CountAsync() > 0)
                    {
                        cafe.Address = await addressAlt.InnerTextAsync();
                    }
                }

                // Extract phone
                var phoneElement = page.Locator("button[data-item-id^='phone']").First;
                if (await phoneElement.CountAsync() > 0)
                {
                    var phoneText = await phoneElement.InnerTextAsync();
                    // Keep only ASCII printable characters (0-9, +, -, spaces, parentheses)
                    phoneText = Regex.Replace(phoneText, @"[^\d\+\-\s\(\)]", "").Trim();
                    // Clean up multiple spaces
                    phoneText = Regex.Replace(phoneText, @"\s+", " ");
                    cafe.Phone = phoneText;
                }

                // Extract website
                var websiteElement = page.Locator("a[data-item-id='authority']").First;
                if (await websiteElement.CountAsync() > 0)
                {
                    cafe.Website = await websiteElement.GetAttributeAsync("href");
                }

                // Extract price level
                var priceElement = page.Locator("span[aria-label*='Price']").First;
                if (await priceElement.CountAsync() > 0)
                {
                    cafe.PriceLevel = await priceElement.InnerTextAsync();
                }

                // Extract opening hours
                cafe.OpeningHours = await ExtractOpeningHoursAsync(page);
                if (!string.IsNullOrEmpty(cafe.OpeningHours))
                {
                    _logger.LogInformation("Extracted opening hours for {Name}", cafe.Name);
                }

                // Extract coordinates from URL
                var coordMatch = Regex.Match(currentUrl, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
                if (coordMatch.Success)
                {
                    if (double.TryParse(coordMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                        cafe.Latitude = lat;
                    if (double.TryParse(coordMatch.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lng))
                        cafe.Longitude = lng;
                }

                // Fallback: Extract coordinates from page script/metadata if URL didn't have them
                if (!cafe.Latitude.HasValue || !cafe.Longitude.HasValue)
                {
                    try
                    {
                        var coords = await page.EvaluateAsync<dynamic>(@"() => {
                            // Try to find coordinates in various places
                            const scripts = Array.from(document.querySelectorAll('script'));
                            for (const script of scripts) {
                                const content = script.textContent || '';
                                // Pattern: [null,null,lat,lng] or similar coordinate arrays
                                const match = content.match(/\[null,null,(-?\d+\.\d+),(-?\d+\.\d+)\]/);
                                if (match) {
                                    return { lat: parseFloat(match[1]), lng: parseFloat(match[2]) };
                                }
                                // Pattern: ""lat"":123.456,""lng"":789.012
                                const match2 = content.match(/""lat""\s*:\s*(-?\d+\.\d+).*""lng""\s*:\s*(-?\d+\.\d+)/);
                                if (match2) {
                                    return { lat: parseFloat(match2[1]), lng: parseFloat(match2[2]) };
                                }
                            }
                            return null;
                        }");

                        if (coords != null)
                        {
                            double? lat = coords.lat;
                            double? lng = coords.lng;
                            if (lat.HasValue && lng.HasValue)
                            {
                                cafe.Latitude = lat.Value;
                                cafe.Longitude = lng.Value;
                                _logger.LogInformation("Extracted coordinates from page script for {Name}: {Lat}, {Lng}", cafe.Name, cafe.Latitude, cafe.Longitude);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Failed to extract coordinates from script for {Name}: {Message}", cafe.Name, ex.Message);
                    }
                }

                cafe.GoogleMapsUrl = currentUrl;

                // Attempt to discover BGG Username
                cafe.BggUsername = await DiscoverBggUsernameAsync(page, cafe.Website, cafe.Name);
                if (!string.IsNullOrEmpty(cafe.BggUsername))
                {
                    _logger.LogInformation("Discovered BGG username: {Username} for {CafeName}", cafe.BggUsername, cafe.Name);
                }

                // Extract Google Place ID from URL
                var placeIdMatch = Regex.Match(currentUrl, @"0x[0-9a-f]+:0x[0-9a-f]+");
                if (placeIdMatch.Success)
                {
                    cafe.GooglePlaceId = placeIdMatch.Value;
                }

                // Extract categories
                var categoryElements = page.Locator("button[jsaction*='category']");
                var categoryCount = await categoryElements.CountAsync();
                for (int i = 0; i < categoryCount; i++)
                {
                    var text = await categoryElements.Nth(i).InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        cafe.Categories.Add(text.Trim());
                    }
                }

                // Extract main image
                try
                {
                    var imageElement = page.Locator(".RZ66Rb.FgCUCc img").First;
                    if (await imageElement.CountAsync() > 0)
                    {
                        var imgSrc = await imageElement.GetAttributeAsync("src");
                        if (!string.IsNullOrEmpty(imgSrc) && imgSrc.StartsWith("http"))
                        {
                            cafe.ImageUrl = imgSrc;

                            // Download and save image locally
                            _logger.LogInformation("Downloading image for {Name}", cafe.Name);
                            cafe.LocalImagePath = await _imageStorageService.DownloadAndSaveImageAsync(imgSrc, "cafes");

                            if (!string.IsNullOrEmpty(cafe.LocalImagePath))
                            {
                                _logger.LogInformation("Image saved locally at {Path}", cafe.LocalImagePath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error extracting image for {Name}", cafe.Name);
                }

                // Extract photos and download them locally
                var (photoUrls, photoLocalPaths) = await ExtractPhotosAsync(page, cafe.Name);
                cafe.PhotoUrls = photoUrls;
                cafe.PhotoLocalPaths = photoLocalPaths;

                // Extract reviews
                cafe.Reviews = await ExtractReviewsAsync(page);

                // Extract About attributes
                var attributes = await ExtractAttributesAsync(page, cafe.Name);
                if (attributes != null && attributes.Any())
                {
                    cafe.Attributes = attributes;
                }

                // Parse city, state, country from address
                ParseAddressComponents(cafe);

                // Crawl Website for Games
                if (!string.IsNullOrEmpty(cafe.Website))
                {
                    try
                    {
                        _logger.LogInformation("Crawling website for games: {Url}", cafe.Website);
                        var games = await _cafeWebsiteCrawlerService.CrawlCafeWebsiteForGamesAsync(cafe.Website);

                        // CafeWebsiteCrawlerService already matches games with whitelist and BGG API
                        // Only games that have BggId are returned, so no need to call BGG API again
                        cafe.FoundGames = games;
                        _logger.LogInformation("Found {Count} games on website for {Name}", games.Count, cafe.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error crawling website games for {Name}", cafe.Name);
                    }
                }

                return cafe;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting cafe data");
                return null;
            }
        }

        private async Task<(List<string> PhotoUrls, List<string> PhotoLocalPaths)> ExtractPhotosAsync(IPage page, string cafeName)
        {
            var photoUrls = new List<string>();
            var photoLocalPaths = new List<string>();
            try
            {
                // Look for Photos tab/button
                var photosTab = page.Locator("button[aria-label*='Photo']").First;
                if (await photosTab.CountAsync() > 0)
                {
                    await photosTab.ClickAsync();
                    await page.WaitForTimeoutAsync(2000);

                    // Wait for photos to load
                    var photoItems = page.Locator("a[data-photo-index] div.U39Pmb");

                    // Specific selector for photo grid items might vary, trying a few common ones
                    if (await photoItems.CountAsync() == 0)
                    {
                         photoItems = page.Locator("div[role='img']");
                    }

                    var count = await photoItems.CountAsync();
                    _logger.LogInformation("Found {Count} potential photos for {Name}", count, cafeName);

                    var extractedUrls = new List<string>();
                    for (int i = 0; i < Math.Min(count, 10); i++)
                    {
                        try
                        {
                            var style = await photoItems.Nth(i).GetAttributeAsync("style");
                            if (!string.IsNullOrEmpty(style))
                            {
                                var match = Regex.Match(style, @"url\(""(.*?)""\)");
                                if (match.Success)
                                {
                                    // Get the URL and upgrade to high resolution
                                    var photoUrl = UpgradeGooglePhotoUrl(match.Groups[1].Value);
                                    extractedUrls.Add(photoUrl);
                                }
                            }
                        }
                        catch
                        {
                            // Ignore individual photo failures
                        }
                    }

                    // Download photos in parallel for speed
                    _logger.LogInformation("Downloading {Count} photos in parallel for {Name}", extractedUrls.Count, cafeName);

                    var downloadTasks = extractedUrls.Select(async (url, index) =>
                    {
                        try
                        {
                            var localPath = await _imageStorageService.DownloadAndSaveImageAsync(url, "photos");
                            if (!string.IsNullOrEmpty(localPath))
                            {
                                return (Url: url, LocalPath: localPath, Index: index);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error downloading photo {Index} for {Name}", index, cafeName);
                        }
                        return (Url: (string?)null, LocalPath: (string?)null, Index: index);
                    }).ToList();

                    var results = await Task.WhenAll(downloadTasks);

                    // Add successful downloads in order
                    foreach (var result in results.OrderBy(r => r.Index))
                    {
                        if (!string.IsNullOrEmpty(result.Url) && !string.IsNullOrEmpty(result.LocalPath))
                        {
                            photoUrls.Add(result.Url);
                            photoLocalPaths.Add(result.LocalPath);
                        }
                    }

                    _logger.LogInformation("Successfully downloaded {Count}/{Total} photos for {Name}",
                        photoUrls.Count, extractedUrls.Count, cafeName);

                    // Go back to main details
                    var backButton = page.Locator("button[aria-label='Back']").First;
                    if (await backButton.CountAsync() > 0)
                    {
                        await backButton.ClickAsync();
                    }
                    else
                    {
                        // Fallback: try clicking the "Overview" tab if available
                         var overviewTab = page.Locator("button[aria-label*='Overview']").First;
                         if (await overviewTab.CountAsync() > 0)
                         {
                             await overviewTab.ClickAsync();
                         }
                    }
                    await page.WaitForTimeoutAsync(1000);
                }
            }
            catch (Exception ex)
            {
                 _logger.LogWarning(ex, "Error extracting photos for {Name}", cafeName);
            }
            return (photoUrls, photoLocalPaths);
        }

        /// <summary>
        /// Upgrades Google Photos URL to higher resolution by modifying size parameters.
        /// Google Photos URLs often contain size parameters like =w100-h100 or =s100.
        /// This method replaces them with larger dimensions for better quality images.
        /// </summary>
        private string UpgradeGooglePhotoUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Pattern 1: =w{width}-h{height} format (e.g., =w100-h100)
            var whPattern = Regex.Match(url, @"=w\d+-h\d+");
            if (whPattern.Success)
            {
                return url.Replace(whPattern.Value, "=w800-h600");
            }

            // Pattern 2: =s{size} format (e.g., =s100)
            var sPattern = Regex.Match(url, @"=s\d+");
            if (sPattern.Success)
            {
                return url.Replace(sPattern.Value, "=s800");
            }

            // Pattern 3: URL ends with size suffix like -w100-h100 or similar
            var suffixPattern = Regex.Match(url, @"-w\d+-h\d+(?=\.[a-zA-Z]+$)");
            if (suffixPattern.Success)
            {
                return url.Replace(suffixPattern.Value, "-w800-h600");
            }

            // If no size parameter found, try to append one if it's a googleusercontent URL
            if (url.Contains("googleusercontent.com") && !url.Contains("="))
            {
                return url + "=w800-h600";
            }

            return url;
        }

        private async Task<List<CrawledReviewData>> ExtractReviewsAsync(IPage page)
        {
            var reviews = new List<CrawledReviewData>();

            try
            {
                // Scroll to reviews section
                await page.EvaluateAsync("() => { const elem = document.querySelector('[role=\"region\"]'); if (elem) elem.scrollIntoView(); }");
                await page.WaitForTimeoutAsync(2000);

                // Click on reviews button to expand reviews
                // Try multiple potential selectors for the Reviews tab
                var reviewsTab = page.Locator("button[role='tab'][aria-label*='Reviews']").First;
                if (await reviewsTab.CountAsync() == 0)
                {
                    reviewsTab = page.Locator("button:has-text('Reviews')").First;
                }

                if (await reviewsTab.CountAsync() > 0)
                {
                    await reviewsTab.ClickAsync();
                    await page.WaitForTimeoutAsync(3000);
                }

                // Scroll reviews panel to load more
                // Scroll reviews panel to load more by finding the scrollable container of the reviews
                // We find the first review item and look for its scrollable parent
                var firstReviewItem = page.Locator("div[data-review-id]").First;
                if (await firstReviewItem.CountAsync() > 0)
                {
                    var scrollableParent = await firstReviewItem.EvaluateHandleAsync(@"element => {
                        let parent = element.parentElement;
                        while (parent) {
                            const style = window.getComputedStyle(parent);
                            if (style.overflowY === 'auto' || style.overflowY === 'scroll') return parent;
                            parent = parent.parentElement;
                        }
                        return null;
                    }");

                    if (scrollableParent != null)
                    {
                        var scrollCount = 0;
                        while (scrollCount < 3)
                        {
                            await scrollableParent.EvaluateAsync("el => el.scrollTop = el.scrollHeight");
                            await page.WaitForTimeoutAsync(1500);
                            scrollCount++;
                        }
                    }
                    else
                    {
                        // Fallback: Try to scroll the last review item into view
                         var reviewItemsList = page.Locator("div[data-review-id]");
                         var count = await reviewItemsList.CountAsync();
                         if (count > 0)
                         {
                             await reviewItemsList.Nth(count - 1).ScrollIntoViewIfNeededAsync();
                             await page.WaitForTimeoutAsync(1500);
                         }
                    }
                }

                // Extract individual reviews
                var reviewItems = page.Locator("div[data-review-id]");
                var reviewCount = await reviewItems.CountAsync();

                _logger.LogInformation("Found {Count} reviews", reviewCount);

                for (int i = 0; i < Math.Min(reviewCount, 10); i++)
                {
                    try
                    {
                        var review = new CrawledReviewData();
                        var reviewItem = reviewItems.Nth(i);

                        // Extract author - Updated selector based on feedback
                        var authorElement = reviewItem.Locator("div.d4r55").First;
                        if (await authorElement.CountAsync() == 0)
                        {
                            authorElement = reviewItem.Locator(".d4qsdf").First; // Fallback
                        }

                        if (await authorElement.CountAsync() > 0)
                        {
                            review.Author = await authorElement.InnerTextAsync();
                        }

                        // Extract rating
                        var ratingElement = reviewItem.Locator("span[role='img'][aria-label*='stars']").First;
                        if (await ratingElement.CountAsync() > 0)
                        {
                            var ariaLabel = await ratingElement.GetAttributeAsync("aria-label");
                            if (ariaLabel != null)
                            {
                                var match = Regex.Match(ariaLabel, @"([\d.]+)\s*stars?");
                                if (match.Success && double.TryParse(match.Groups[1].Value, out var rating))
                                {
                                    review.Rating = rating;
                                }
                            }
                        }

                        // Click "More" button to expand full review text if available
                        var moreButton = reviewItem.Locator("button.w8nwRe.kyuRq[aria-label='See more']").First;
                        if (await moreButton.CountAsync() == 0)
                        {
                            // Fallback selectors for More button
                            moreButton = reviewItem.Locator("button[aria-label='See more']").First;
                        }
                        if (await moreButton.CountAsync() == 0)
                        {
                            moreButton = reviewItem.Locator("button:has-text('More')").First;
                        }

                        if (await moreButton.CountAsync() > 0)
                        {
                            try
                            {
                                await moreButton.ClickAsync();
                                await page.WaitForTimeoutAsync(500); // Wait for text to expand
                            }
                            catch (Exception clickEx)
                            {
                                _logger.LogDebug("Could not click More button: {Message}", clickEx.Message);
                            }
                        }

                        // Extract review text - Updated selector
                        var textElement = reviewItem.Locator("span.wiI7pd").First;
                        if (await textElement.CountAsync() == 0)
                        {
                             textElement = reviewItem.Locator(".MyEned").First;
                        }

                        if (await textElement.CountAsync() > 0)
                        {
                            review.ReviewText = await textElement.InnerTextAsync();
                        }

                        // Extract review date - Updated selector
                        var dateElement = reviewItem.Locator("span.rsqaWe").First;
                        if (await dateElement.CountAsync() == 0)
                        {
                             dateElement = reviewItem.Locator(".p5KrLc").First;
                        }

                        if (await dateElement.CountAsync() > 0)
                        {
                            var dateText = await dateElement.InnerTextAsync();
                            if (TryParseRelativeDate(dateText, out var date))
                            {
                                review.ReviewDate = date;
                            }
                        }

                        // Extract helpful count
                        var helpfulElement = reviewItem.Locator("button[aria-label*='helpful']").First;
                        if (await helpfulElement.CountAsync() > 0)
                        {
                            var helpfulText = await helpfulElement.InnerTextAsync();
                            if (int.TryParse(Regex.Match(helpfulText, @"\d+").Value, out var helpful))
                            {
                                review.HelpfulCount = helpful;
                            }
                        }

                        if (!string.IsNullOrEmpty(review.Author) && !string.IsNullOrEmpty(review.ReviewText))
                        {
                            reviews.Add(review);
                            _logger.LogInformation("Extracted review by: {Author}", review.Author);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract review {Index}", i);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting reviews");
            }

            return reviews;
        }

        private bool TryParseRelativeDate(string dateText, out DateTime date)
        {
            date = DateTime.UtcNow;

            if (string.IsNullOrEmpty(dateText))
                return false;

            dateText = dateText.ToLower().Trim();

            // Parse "X days ago", "X months ago", etc.
            if (Regex.Match(dateText, @"(\d+)\s*days?\s*ago").Success)
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (int.TryParse(match.Value, out var days))
                {
                    date = DateTime.UtcNow.AddDays(-days);
                    return true;
                }
            }
            else if (Regex.Match(dateText, @"(\d+)\s*weeks?\s*ago").Success)
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (int.TryParse(match.Value, out var weeks))
                {
                    date = DateTime.UtcNow.AddDays(-weeks * 7);
                    return true;
                }
            }
             else if (Regex.Match(dateText, @"(\d+)\s*months?\s*ago").Success)
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (int.TryParse(match.Value, out var months))
                {
                    date = DateTime.UtcNow.AddMonths(-months);
                    return true;
                }
            }
            else if (Regex.Match(dateText, @"(\d+)\s*years?\s*ago").Success)
            {
                 var match = Regex.Match(dateText, @"(\d+)");
                if (int.TryParse(match.Value, out var years))
                {
                    date = DateTime.UtcNow.AddYears(-years);
                    return true;
                }
            }
            else if (dateText.Contains("today"))
            {
                date = DateTime.UtcNow;
                return true;
            }
            else if (dateText.Contains("yesterday"))
            {
                date = DateTime.UtcNow.AddDays(-1);
                return true;
            }

            return false;
        }

        private async Task<Dictionary<string, List<string>>> ExtractAttributesAsync(IPage page, string cafeName)
        {
            var attributes = new Dictionary<string, List<string>>();
            try
            {
                // Go to About tab
                var aboutTab = page.Locator("button[aria-label*='About']").First;
                if (await aboutTab.CountAsync() > 0)
                {
                    await aboutTab.ClickAsync();
                    await page.WaitForTimeoutAsync(2000);

                    // Find all H2 headers which represent categories (Accessibility, Amenities, etc.)
                    // Common structure: h2.fontTitleSmall -> ul -> li
                    var headers = page.Locator("h2.fontTitleSmall");
                    var count = await headers.CountAsync();

                    for (int i = 0; i < count; i++)
                    {
                        var header = headers.Nth(i);
                        var category = await header.InnerTextAsync();
                        if (string.IsNullOrWhiteSpace(category)) continue;

                        // Find the sibling UL or the container holding the items
                        // The structure provided shows: div.iP2t7d > h2 + ul
                        // So we look for the list in the same container or sibling
                        
                        // Strategy: Get parent, then find ul
                        var sectionValues = new List<string>();
                        var parent = header.Locator("xpath=..");
                        var items = parent.Locator("ul > li");
                        
                        var itemsCount = await items.CountAsync();
                        for (int j = 0; j < itemsCount; j++)
                        {
                            var item = items.Nth(j);
                            var text = await item.InnerTextAsync(); 
                            // Or try aria-label if text is hidden/icon only
                            if (string.IsNullOrWhiteSpace(text))
                            {
                                var spanLabel = item.Locator("span[aria-label]").First;
                                if (await spanLabel.CountAsync() > 0) 
                                {
                                    text = await spanLabel.GetAttributeAsync("aria-label");
                                }
                            }
                            
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // Remove checkmark symbols if present (often raw text includes unicode)
                                text = text.Replace("", "").Trim();
                                sectionValues.Add(text);
                            }
                        }

                        if (sectionValues.Any())
                        {
                            attributes[category] = sectionValues;
                        }
                    }

                    _logger.LogInformation("Extracted attributes for {Name}: {Count} categories", cafeName, attributes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting attributes for {Name}", cafeName);
            }
            return attributes;
        }

        public Cafe ConvertToCafe(CrawledCafeData data, string city, string? state = null)
        {
            var cafe = new Cafe
            {
                Name = data.Name,
                Address = data.Address,
                City = city,
                State = state,
                Country = "United States",
                Latitude = data.Latitude ?? 0,
                Longitude = data.Longitude ?? 0,
                Phone = data.Phone,
                Website = data.Website,
                AverageRating = data.Rating.HasValue ? (decimal)data.Rating.Value : null,
                TotalReviews = data.ReviewCount ?? 0,
                PriceRange = data.PriceLevel,
                Slug = GenerateSlug(data.Name),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (data.Attributes != null && data.Attributes.Any())
            {
                cafe.SetAttributes(data.Attributes);
            }

            return cafe;
        }

        public Task SaveCrawledReviewsAsync(int cafeId, List<CrawledReviewData> crawledReviews)
        {
            // This method will be implemented in the service that has DB context access
            // For now, we'll just log
            _logger.LogInformation("Prepared {Count} reviews for cafe {CafeId}", crawledReviews.Count, cafeId);
            return Task.CompletedTask;
        }

        public List<BoardGameCafeFinder.Models.Domain.Review> ConvertToReviews(int cafeId, List<CrawledReviewData> crawledReviews, int? systemUserId = null)
        {
            var reviews = new List<BoardGameCafeFinder.Models.Domain.Review>();

            foreach (var crawledReview in crawledReviews)
            {
                reviews.Add(new BoardGameCafeFinder.Models.Domain.Review
                {
                    CafeId = cafeId,
                    UserId = systemUserId, // System user for crawled reviews (can be null)
                    Rating = (int)Math.Clamp(Math.Round(crawledReview.Rating), 1, 5),
                    Title = $"Google Maps Review - {crawledReview.Author}",
                    Content = crawledReview.ReviewText,
                    VisitDate = crawledReview.ReviewDate,
                    IsVerifiedVisit = false,
                    HelpfulCount = crawledReview.HelpfulCount ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return reviews;
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');
            return slug;
        }

        private void ParseAddressComponents(CrawledCafeData cafe)
        {
            if (string.IsNullOrEmpty(cafe.Address))
                return;

            // Clean address (remove Unicode control characters)
            var cleanAddress = Regex.Replace(cafe.Address, @"[\uE000-\uF8FF]", "").Trim();
            cafe.Address = cleanAddress;

            // Try to parse US address format: "City, State ZIP, Country"
            var parts = cleanAddress.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();

            if (parts.Count >= 2)
            {
                // Last part is usually country
                cafe.Country = parts[^1];

                // Second to last contains state and ZIP
                if (parts.Count >= 3)
                {
                    var stateZipPart = parts[^2];
                    var stateMatch = Regex.Match(stateZipPart, @"^([A-Z]{2})\s+\d");
                    if (stateMatch.Success)
                    {
                        cafe.State = stateMatch.Groups[1].Value;
                    }
                    else
                    {
                        // For non-US addresses (Vietnam, etc.)
                        cafe.State = stateZipPart;
                    }

                    // City is third from last (if exists)
                    if (parts.Count >= 4)
                    {
                        cafe.City = parts[^3];
                    }
                    else
                    {
                        // If only 3 parts: "Street, City State ZIP, Country" - extract city from stateZipPart
                        var cityFromState = Regex.Match(stateZipPart, @"^(.+?)\s+[A-Z]{2}\s+\d");
                        if (cityFromState.Success)
                        {
                            cafe.City = cityFromState.Groups[1].Value.Trim();
                        }
                        else
                        {
                            // Use second to last as city fallback
                            cafe.City = stateZipPart;
                        }
                    }
                }
                else
                {
                    // Only 2 parts: "City, Country"
                    cafe.City = parts[^2];
                }
            }
            else if (parts.Count == 1)
            {
                // Single part - use as city
                cafe.City = parts[0];
            }

            // Clean up city name (remove ZIP codes and extra whitespace)
            if (!string.IsNullOrEmpty(cafe.City))
            {
                cafe.City = Regex.Replace(cafe.City, @"\s+\d{5}(-\d{4})?$", "").Trim();
            }
        }
        private async Task<string?> DiscoverBggUsernameAsync(IPage googleMapsPage, string? websiteUrl, string cafeName)
        {
            // Patterns to match BGG user URLs
            // Priority: collection/user > user > profile
            const string bggCollectionPattern = @"boardgamegeek\.com/collection/user/([A-Za-z0-9_-]+)";
            const string bggUserPattern = @"boardgamegeek\.com/user/([A-Za-z0-9_-]+)";
            const string bggProfilePattern = @"boardgamegeek\.com/profile/([A-Za-z0-9_-]+)";

            try
            {
                // Method 1: Look for BGG links in Google Maps page content
                var pageContent = await googleMapsPage.ContentAsync();

                // Check for collection/user pattern first (most reliable)
                var collectionMatch = Regex.Match(pageContent, bggCollectionPattern, RegexOptions.IgnoreCase);
                if (collectionMatch.Success)
                {
                    _logger.LogInformation("Found BGG collection link in Google Maps page content: {Username}", collectionMatch.Groups[1].Value);
                    return collectionMatch.Groups[1].Value;
                }

                // Check for /user/ pattern
                var userMatch = Regex.Match(pageContent, bggUserPattern, RegexOptions.IgnoreCase);
                if (userMatch.Success)
                {
                    _logger.LogInformation("Found BGG user link in Google Maps page content: {Username}", userMatch.Groups[1].Value);
                    return userMatch.Groups[1].Value;
                }

                // Check for /profile/ pattern
                var profileMatch = Regex.Match(pageContent, bggProfilePattern, RegexOptions.IgnoreCase);
                if (profileMatch.Success)
                {
                    _logger.LogInformation("Found BGG profile link in Google Maps page content: {Username}", profileMatch.Groups[1].Value);
                    return profileMatch.Groups[1].Value;
                }

                // Method 2: Look for BGG links via locator (clickable links)
                var bggLinks = await googleMapsPage.Locator("a[href*='boardgamegeek.com']").AllAsync();
                foreach (var link in bggLinks)
                {
                    var href = await link.GetAttributeAsync("href");
                    if (string.IsNullOrEmpty(href)) continue;

                    var match = Regex.Match(href, bggCollectionPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _logger.LogInformation("Found BGG collection link on Google Maps: {Href}", href);
                        return match.Groups[1].Value;
                    }

                    match = Regex.Match(href, bggUserPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _logger.LogInformation("Found BGG user link on Google Maps: {Href}", href);
                        return match.Groups[1].Value;
                    }

                    match = Regex.Match(href, bggProfilePattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        _logger.LogInformation("Found BGG profile link on Google Maps: {Href}", href);
                        return match.Groups[1].Value;
                    }
                }

                // Method 3: Try to find BGG link on cafe's website (if available)
                if (!string.IsNullOrEmpty(websiteUrl))
                {
                    var bggUsername = await SearchWebsiteForBggAsync(googleMapsPage.Context, websiteUrl);
                    if (!string.IsNullOrEmpty(bggUsername))
                    {
                        return bggUsername;
                    }
                }

                // Method 4: Search Google for BGG collection page
                var searchUsername = await SearchGoogleForBggAsync(googleMapsPage.Context, cafeName);
                if (!string.IsNullOrEmpty(searchUsername))
                {
                    return searchUsername;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error discovering BGG username: {Message}", ex.Message);
                return null;
            }
        }

        private async Task<string?> SearchWebsiteForBggAsync(IBrowserContext context, string websiteUrl)
        {
            const string bggCollectionPattern = @"boardgamegeek\.com/collection/user/([A-Za-z0-9_-]+)";
            const string bggUserPattern = @"boardgamegeek\.com/user/([A-Za-z0-9_-]+)";
            const string bggProfilePattern = @"boardgamegeek\.com/profile/([A-Za-z0-9_-]+)";

            IPage? websitePage = null;
            try
            {
                _logger.LogInformation("Checking website for BGG link: {Url}", websiteUrl);
                websitePage = await context.NewPageAsync();
                await websitePage.GotoAsync(websiteUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 15000
                });

                // Get full page content and search for BGG patterns
                var content = await websitePage.ContentAsync();

                var username = ExtractBggUsername(content, bggCollectionPattern, bggUserPattern, bggProfilePattern);
                if (!string.IsNullOrEmpty(username))
                {
                    _logger.LogInformation("Found BGG username on website homepage: {Username}", username);
                    return username;
                }

                // Also check common pages like /games, /library, /about, /boardgames
                var pagesToCheck = new[] { "/games", "/library", "/about", "/boardgames", "/board-games", "/game-library" };
                var baseUri = new Uri(websiteUrl);

                foreach (var pagePath in pagesToCheck)
                {
                    try
                    {
                        var pageUrl = new Uri(baseUri, pagePath).ToString();
                        await websitePage.GotoAsync(pageUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 10000
                        });

                        content = await websitePage.ContentAsync();

                        username = ExtractBggUsername(content, bggCollectionPattern, bggUserPattern, bggProfilePattern);
                        if (!string.IsNullOrEmpty(username))
                        {
                            _logger.LogInformation("Found BGG username on {PageUrl}: {Username}", pageUrl, username);
                            return username;
                        }
                    }
                    catch
                    {
                        // Page doesn't exist, continue to next
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to check website for BGG: {Message}", ex.Message);
                return null;
            }
            finally
            {
                if (websitePage != null)
                {
                    await websitePage.CloseAsync();
                }
            }
        }

        private async Task<string?> SearchGoogleForBggAsync(IBrowserContext context, string cafeName)
        {
            const string bggCollectionPattern = @"boardgamegeek\.com/collection/user/([A-Za-z0-9_-]+)";

            IPage? searchPage = null;
            try
            {
                // Search Google for: "cafe name" site:boardgamegeek.com/collection/user
                var searchQuery = $"\"{cafeName}\" site:boardgamegeek.com/collection/user";
                var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(searchQuery)}";

                _logger.LogInformation("Searching Google for BGG collection: {Query}", searchQuery);

                searchPage = await context.NewPageAsync();
                await searchPage.GotoAsync(searchUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 15000
                });

                await searchPage.WaitForTimeoutAsync(2000);

                // Look for BGG collection links in search results
                var searchContent = await searchPage.ContentAsync();
                var matches = Regex.Matches(searchContent, bggCollectionPattern, RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var username = match.Groups[1].Value;
                    // Skip common false positives
                    if (username.ToLower() != "user" && username.Length > 2)
                    {
                        _logger.LogInformation("Found BGG collection via Google search: {Username}", username);
                        return username;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to search Google for BGG: {Message}", ex.Message);
                return null;
            }
            finally
            {
                if (searchPage != null)
                {
                    await searchPage.CloseAsync();
                }
            }
        }

        private string? ExtractBggUsername(string content, string collectionPattern, string userPattern, string profilePattern)
        {
            // Priority: collection/user > user > profile
            var collectionMatch = Regex.Match(content, collectionPattern, RegexOptions.IgnoreCase);
            if (collectionMatch.Success)
            {
                var username = collectionMatch.Groups[1].Value;
                if (IsValidBggUsername(username))
                    return username;
            }

            var userMatch = Regex.Match(content, userPattern, RegexOptions.IgnoreCase);
            if (userMatch.Success)
            {
                var username = userMatch.Groups[1].Value;
                if (IsValidBggUsername(username))
                    return username;
            }

            var profileMatch = Regex.Match(content, profilePattern, RegexOptions.IgnoreCase);
            if (profileMatch.Success)
            {
                var username = profileMatch.Groups[1].Value;
                if (IsValidBggUsername(username))
                    return username;
            }

            return null;
        }

        private bool IsValidBggUsername(string username)
        {
            if (string.IsNullOrEmpty(username) || username.Length < 2)
                return false;

            // Skip common false positives
            var invalidNames = new[] { "user", "profile", "collection", "boardgame", "geek" };
            return !invalidNames.Contains(username.ToLowerInvariant());
        }

        private async Task<string?> ExtractOpeningHoursAsync(IPage page)
        {
            try
            {
                // First, try to click on hours button to expand the hours table
                var hoursButtonSelectors = new[]
                {
                    "button[data-item-id='oh']",
                    "button[aria-label*='hour']",
                    "[data-item-id='oh']",
                    ".OqCZI button", // Hours section button
                    "div[aria-label*='Opens'] button",
                    "div[aria-label*='Closes'] button"
                };

                foreach (var selector in hoursButtonSelectors)
                {
                    try
                    {
                        var hoursButton = page.Locator(selector).First;
                        if (await hoursButton.CountAsync() > 0 && await hoursButton.IsVisibleAsync())
                        {
                            await hoursButton.ClickAsync();
                            await page.WaitForTimeoutAsync(1500);
                            _logger.LogDebug("Clicked hours button with selector: {Selector}", selector);
                            break;
                        }
                    }
                    catch
                    {
                        // Try next selector
                    }
                }

                // Method 1: Extract from table rows using multiple selectors
                var tableSelectors = new[]
                {
                    "table.eK4R0e tbody tr.y0skZc",
                    "table.eK4R0e tr.y0skZc",
                    "table tbody tr.y0skZc",
                    ".t39EBf table tr", // Alternative hours table
                    "table.WgFkxc tr"
                };

                foreach (var tableSelector in tableSelectors)
                {
                    var rows = page.Locator(tableSelector);
                    var rowCount = await rows.CountAsync();
                    _logger.LogDebug("Table selector '{Selector}' found {Count} rows", tableSelector, rowCount);

                    if (rowCount >= 7) // Should have 7 days
                    {
                        var hoursList = new List<string>();

                        for (int i = 0; i < rowCount && i < 7; i++)
                        {
                            var row = rows.Nth(i);

                            // Try multiple selectors for day name
                            string day = "";
                            var daySelectors = new[] { "td.ylH6lf div", "td:first-child div", "td:first-child" };
                            foreach (var daySelector in daySelectors)
                            {
                                var dayElement = row.Locator(daySelector).First;
                                if (await dayElement.CountAsync() > 0)
                                {
                                    day = (await dayElement.InnerTextAsync()).Trim();
                                    if (!string.IsNullOrEmpty(day)) break;
                                }
                            }

                            // Try multiple ways to get hours
                            string hoursText = "";

                            // Try aria-label on hours cell
                            var hoursCell = row.Locator("td.mxowUb, td:nth-child(2)").First;
                            if (await hoursCell.CountAsync() > 0)
                            {
                                var ariaLabel = await hoursCell.GetAttributeAsync("aria-label");
                                if (!string.IsNullOrEmpty(ariaLabel))
                                {
                                    hoursText = ariaLabel;
                                }
                            }

                            // Fallback: try li.G8aQO
                            if (string.IsNullOrEmpty(hoursText))
                            {
                                var timeElements = row.Locator("li.G8aQO");
                                var timeCount = await timeElements.CountAsync();
                                if (timeCount > 0)
                                {
                                    var times = new List<string>();
                                    for (int j = 0; j < timeCount; j++)
                                    {
                                        var text = (await timeElements.Nth(j).InnerTextAsync()).Trim();
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            times.Add(text);
                                        }
                                    }
                                    hoursText = string.Join(", ", times);
                                }
                            }

                            // Fallback: try inner text of second cell
                            if (string.IsNullOrEmpty(hoursText))
                            {
                                var secondCell = row.Locator("td:nth-child(2)").First;
                                if (await secondCell.CountAsync() > 0)
                                {
                                    hoursText = (await secondCell.InnerTextAsync()).Trim();
                                }
                            }

                            if (!string.IsNullOrEmpty(day) && !string.IsNullOrEmpty(hoursText))
                            {
                                hoursList.Add($"{day}: {hoursText}");
                            }
                        }

                        if (hoursList.Count >= 5) // At least 5 days found
                        {
                            _logger.LogDebug("Extracted {Count} days of opening hours", hoursList.Count);
                            return string.Join("; ", hoursList);
                        }
                    }
                }

                // Method 2: Try aria-label from the hours section (may contain all info)
                var ariaSelectors = new[]
                {
                    "[aria-label*='Monday'][aria-label*='Tuesday']",
                    "[aria-label*='Sunday'][aria-label*='Monday']",
                    ".t39EBf[aria-label]",
                    "[data-item-id='oh'][aria-label]"
                };

                foreach (var selector in ariaSelectors)
                {
                    var element = page.Locator(selector).First;
                    if (await element.CountAsync() > 0)
                    {
                        var ariaLabel = await element.GetAttributeAsync("aria-label");
                        if (!string.IsNullOrEmpty(ariaLabel) && ariaLabel.Length > 30)
                        {
                            _logger.LogDebug("Extracted hours from aria-label");
                            return ariaLabel;
                        }
                    }
                }

                // Method 3: Get raw text from hours section
                var hoursSectionSelectors = new[]
                {
                    ".t39EBf.GUrTXd",
                    "div[data-attrid*='hours']",
                    ".OqCZI"
                };

                foreach (var selector in hoursSectionSelectors)
                {
                    var section = page.Locator(selector).First;
                    if (await section.CountAsync() > 0)
                    {
                        var text = (await section.InnerTextAsync()).Trim();
                        if (!string.IsNullOrEmpty(text) && text.Length > 20 && text.Contains(":"))
                        {
                            // Clean up the text - replace newlines with semicolons
                            text = Regex.Replace(text, @"\s*\n\s*", "; ");
                            text = Regex.Replace(text, @";\s*;", ";");
                            _logger.LogDebug("Extracted hours from section text");
                            return text.Trim();
                        }
                    }
                }

                _logger.LogDebug("Could not extract opening hours");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error extracting opening hours: {Message}", ex.Message);
                return null;
            }
        }
    }
}
