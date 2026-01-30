using Microsoft.Playwright;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Services
{
    public interface IGoogleMapsCrawlerService
    {
        Task<List<CrawledVenueData>> CrawlEscapeRoomsAsync(string location, int maxResults = 20);
        Task<List<CrawledVenueData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10);
        Task SaveCrawledReviewsAsync(int venueId, List<CrawledReviewData> crawledReviews);
        List<Review> ConvertToReviews(int venueId, List<CrawledReviewData> crawledReviews, int? systemUserId = null);
    }

    public class CrawledReviewData
    {
        public string Author { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime? ReviewDate { get; set; }
        public int? HelpfulCount { get; set; }
    }

    public class CrawledVenueData
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
        public string? Description { get; set; }
        public List<CrawledRoomData> Rooms { get; set; } = new();
        public List<CrawledReviewData> Reviews { get; set; } = new();
    }

    public class CrawledRoomData
    {
        public string Name { get; set; } = string.Empty;
        public string? Theme { get; set; }
        public int? Difficulty { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public int? DurationMinutes { get; set; }
        public decimal? Price { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class GoogleMapsCrawlerService : IGoogleMapsCrawlerService
    {
        private readonly ILogger<GoogleMapsCrawlerService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public GoogleMapsCrawlerService(
            ILogger<GoogleMapsCrawlerService> logger,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _context = context;
            _environment = environment;
        }

        public async Task<List<CrawledVenueData>> CrawlEscapeRoomsAsync(string location, int maxResults = 20)
        {
            var queries = new[]
            {
                $"escape rooms in {location}",
                $"escape room near {location}",
                $"escape game {location}"
            };

            return await CrawlWithMultipleQueriesAsync(location, queries, maxResults / queries.Length + 1);
        }

        public async Task<List<CrawledVenueData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10)
        {
            var allResults = new List<CrawledVenueData>();
            var seenPlaceIds = new HashSet<string>();

            _logger.LogInformation("╔════════════════════════════════════════════════════════════╗");
            _logger.LogInformation("║ PLAYWRIGHT CRAWLER - Starting browser initialization       ║");
            _logger.LogInformation("╚════════════════════════════════════════════════════════════╝");

            using var playwright = await Playwright.CreateAsync();
            _logger.LogInformation("✓ Playwright instance created");

            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-blink-features=AutomationControlled", "--no-sandbox" }
            });
            _logger.LogInformation("✓ Chromium browser launched (headless mode)");

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
                Locale = "en-US",
                TimezoneId = "America/New_York",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });
            _logger.LogInformation("✓ Browser context created with custom user agent");

            var page = await context.NewPageAsync();
            _logger.LogInformation("✓ New page opened");

            // Handle Google consent dialog if it appears
            await HandleGoogleConsentAsync(page);

            foreach (var query in queries)
            {
                try
                {
                    _logger.LogInformation("────────────────────────────────────────────────────────────");
                    _logger.LogInformation("🔍 Searching Google Maps for: {Query}", query);

                    var searchUrl = $"https://www.google.com/maps/search/{Uri.EscapeDataString(query)}";
                    _logger.LogInformation("   URL: {Url}", searchUrl);

                    await page.GotoAsync(searchUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
                    _logger.LogInformation("   ✓ Page loaded successfully");

                    // Handle any consent dialogs that might appear
                    await HandleGoogleConsentAsync(page);

                    // Wait a bit for dynamic content
                    await page.WaitForTimeoutAsync(3000);

                    // Debug: Log page title and check for errors
                    var pageTitle = await page.TitleAsync();
                    _logger.LogInformation("   📄 Page title: {Title}", pageTitle);

                    // Take a debug screenshot
                    var screenshotPath = Path.Combine(_environment.WebRootPath, "debug", $"crawl-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
                    _logger.LogInformation("   📸 Debug screenshot saved: {Path}", screenshotPath);

                    // Wait for results to load - try multiple selectors
                    bool feedFound = false;
                    var feedSelectors = new[] { "div[role='feed']", "div[role='main']", ".m6QErb.DxyBCb" };

                    foreach (var selector in feedSelectors)
                    {
                        try
                        {
                            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = 5000 });
                            _logger.LogInformation("   ✓ Found container with selector: {Selector}", selector);
                            feedFound = true;
                            break;
                        }
                        catch (TimeoutException)
                        {
                            _logger.LogDebug("   ⊘ Selector not found: {Selector}", selector);
                        }
                    }

                    if (!feedFound)
                    {
                        _logger.LogWarning("   ⚠ No results container found - checking page HTML...");
                        var bodyHtml = await page.Locator("body").InnerHTMLAsync();
                        _logger.LogWarning("   📜 Body length: {Length} chars", bodyHtml.Length);

                        // Check for common blocking scenarios
                        if (bodyHtml.Contains("consent") || bodyHtml.Contains("Accept all"))
                        {
                            _logger.LogWarning("   ⚠ Consent dialog may be blocking - attempting to dismiss...");
                            await HandleGoogleConsentAsync(page);
                            await page.WaitForTimeoutAsync(2000);
                        }
                        continue;
                    }

                    // Scroll to load more results
                    _logger.LogInformation("   📜 Scrolling to load more results...");
                    for (int i = 0; i < 5; i++)
                    {
                        await page.EvaluateAsync(@"() => {
                            const feed = document.querySelector('div[role=""feed""]') || document.querySelector('.m6QErb');
                            if (feed) feed.scrollBy(0, 1000);
                        }");
                        await page.WaitForTimeoutAsync(1500);
                    }
                    _logger.LogInformation("   ✓ Scrolling completed");

                    // Get all place links - try multiple selectors
                    var placeLinks = await page.Locator("a[href*='/maps/place/']").AllAsync();
                    _logger.LogInformation("   📍 Found {Count} place links with href selector", placeLinks.Count);

                    // If no links found, try alternative selectors
                    if (placeLinks.Count == 0)
                    {
                        placeLinks = await page.Locator("a.hfpxzc").AllAsync();
                        _logger.LogInformation("   📍 Found {Count} place links with .hfpxzc selector", placeLinks.Count);
                    }

                    if (placeLinks.Count == 0)
                    {
                        // Try to find any clickable items in the results
                        var resultItems = await page.Locator("div.Nv2PK").AllAsync();
                        _logger.LogInformation("   📍 Found {Count} result items with .Nv2PK selector", resultItems.Count);
                    }

                    int count = 0;
                    foreach (var link in placeLinks)
                    {
                        if (count >= maxResultsPerQuery)
                        {
                            _logger.LogInformation("   ⏹ Reached max results ({Max}) for this query", maxResultsPerQuery);
                            break;
                        }

                        try
                        {
                            var href = await link.GetAttributeAsync("href");
                            if (string.IsNullOrEmpty(href)) continue;

                            // Extract venue name from aria-label BEFORE clicking
                            var ariaLabel = await link.GetAttributeAsync("aria-label");
                            var venueName = ariaLabel ?? "";
                            _logger.LogInformation("   [{Index}] Found venue: {Name}", count + 1, venueName);

                            // Extract place ID from URL
                            var placeIdMatch = Regex.Match(href, @"!1s([^!]+)");
                            var placeId = placeIdMatch.Success ? placeIdMatch.Groups[1].Value : href;

                            if (seenPlaceIds.Contains(placeId))
                            {
                                _logger.LogDebug("   ⊘ Skipping duplicate place ID: {PlaceId}", placeId);
                                continue;
                            }
                            seenPlaceIds.Add(placeId);

                            _logger.LogInformation("   [{Index}] Clicking venue to view details...", count + 1);

                            // Click to view details
                            await link.ClickAsync();
                            await page.WaitForTimeoutAsync(2500);

                            // Wait for details panel to load (look for the detail panel indicators)
                            try
                            {
                                await page.WaitForSelectorAsync("button[data-item-id='address'], div[data-attrid='kc:/location/location:address']", new PageWaitForSelectorOptions { Timeout = 5000 });
                            }
                            catch (TimeoutException)
                            {
                                _logger.LogWarning("   ⚠ Venue details panel didn't load");
                                // Continue anyway and try to extract what we can
                            }

                            var venueData = await ExtractVenueDataAsync(page, venueName);
                            if (venueData != null && !string.IsNullOrEmpty(venueData.Name) && venueData.Name != "Results" && venueData.Name != "Sponsored?")
                            {
                                venueData.GooglePlaceId = placeId;
                                venueData.GoogleMapsUrl = href;

                                // Download image if available
                                if (!string.IsNullOrEmpty(venueData.ImageUrl))
                                {
                                    _logger.LogInformation("       📷 Downloading image...");
                                    venueData.LocalImagePath = await DownloadImageAsync(venueData.ImageUrl, venueData.Name);
                                }

                                allResults.Add(venueData);
                                count++;
                                _logger.LogInformation("   ✓ [{Index}] Extracted: {Name}", count, venueData.Name);
                                _logger.LogInformation("       📍 Address: {Address}", venueData.Address);
                                _logger.LogInformation("       ⭐ Rating: {Rating} ({Reviews} reviews)", venueData.Rating?.ToString("F1") ?? "N/A", venueData.ReviewCount ?? 0);
                                if (!string.IsNullOrEmpty(venueData.Website))
                                    _logger.LogInformation("       🌐 Website: {Website}", venueData.Website);
                            }
                            else
                            {
                                _logger.LogInformation("   ✗ Venue filtered out (not an escape room or missing data)");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("   ⚠ Error extracting place: {Message}", ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("✗ Error crawling query '{Query}': {Message}", query, ex.Message);
                }
            }

            _logger.LogInformation("════════════════════════════════════════════════════════════");
            _logger.LogInformation("🏁 CRAWL COMPLETE: Total venues extracted: {Count}", allResults.Count);
            _logger.LogInformation("════════════════════════════════════════════════════════════");

            return allResults;
        }

        private async Task<CrawledVenueData?> ExtractVenueDataAsync(IPage page, string? preExtractedName = null)
        {
            try
            {
                var venue = new CrawledVenueData();

                // Use pre-extracted name if provided (from aria-label), otherwise try to extract from page
                if (!string.IsNullOrEmpty(preExtractedName))
                {
                    venue.Name = preExtractedName;
                    _logger.LogDebug("       Using pre-extracted name: {Name}", venue.Name);
                }
                else
                {
                    // Name - try multiple selectors
                    var nameSelectors = new[] { "h1.DUwDvf", "div.qBF1Pd.fontHeadlineSmall", "h1" };
                    foreach (var selector in nameSelectors)
                    {
                        try
                        {
                            var nameElement = page.Locator(selector).First;
                            if (await nameElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                            {
                                var extractedName = await nameElement.TextContentAsync() ?? string.Empty;
                                // Skip generic names
                                if (!string.IsNullOrEmpty(extractedName) && extractedName != "Results" && extractedName != "Sponsored?")
                                {
                                    venue.Name = extractedName;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                _logger.LogDebug("       Final name: {Name}", venue.Name);

                // Get category to check if it's an escape room
                var nameAndCategory = venue.Name.ToLower();
                var categorySelectors = new[] { "button[jsaction*='category']", "span.DkEaL", "button.DkEaL" };
                foreach (var selector in categorySelectors)
                {
                    try
                    {
                        var categoryElement = page.Locator(selector).First;
                        if (await categoryElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            var category = await categoryElement.TextContentAsync() ?? "";
                            nameAndCategory += " " + category.ToLower();
                            _logger.LogDebug("       Category: {Category}", category);
                            break;
                        }
                    }
                    catch { }
                }

                // Less strict filtering - accept if query was for escape rooms
                if (!IsLikelyEscapeRoom(nameAndCategory))
                {
                    _logger.LogDebug("       Not an escape room based on: {Text}", nameAndCategory);
                    // Still return it for debugging - let the caller decide
                    // return null;
                }

                // Rating - try multiple selectors
                var ratingSelectors = new[] { "div.F7nice span[aria-hidden='true']", "span.ceNzKf", "div.fontDisplayLarge" };
                foreach (var selector in ratingSelectors)
                {
                    try
                    {
                        var ratingElement = page.Locator(selector).First;
                        if (await ratingElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            var ratingText = await ratingElement.TextContentAsync();
                            if (double.TryParse(ratingText?.Replace(",", ".").Trim(), out double rating) && rating <= 5)
                            {
                                venue.Rating = rating;
                                _logger.LogDebug("       Rating: {Rating}", rating);
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // Review count
                try
                {
                    var reviewElements = await page.Locator("span:has-text('review'), span:has-text('reviews')").AllAsync();
                    foreach (var el in reviewElements)
                    {
                        var text = await el.TextContentAsync();
                        var match = Regex.Match(text ?? "", @"([\d,]+)\s*review");
                        if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out int reviewCount))
                        {
                            venue.ReviewCount = reviewCount;
                            _logger.LogDebug("       Review count: {Count}", reviewCount);
                            break;
                        }
                    }
                }
                catch { }

                // Address - try multiple selectors
                var addressSelectors = new[] {
                    "button[data-item-id='address'] div.fontBodyMedium",
                    "button[data-item-id='address']",
                    "div[data-tooltip*='Copy address']",
                    "button:has(span:has-text('Address'))"
                };
                foreach (var selector in addressSelectors)
                {
                    try
                    {
                        var addressElement = page.Locator(selector).First;
                        if (await addressElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            venue.Address = await addressElement.TextContentAsync() ?? string.Empty;
                            venue.Address = venue.Address.Replace("Address:", "").Trim();
                            if (!string.IsNullOrEmpty(venue.Address))
                            {
                                _logger.LogDebug("       Address: {Address}", venue.Address);
                                break;
                            }
                        }
                    }
                    catch { }
                }

                // Phone
                var phoneSelectors = new[] { "button[data-item-id*='phone'] div.fontBodyMedium", "button[data-item-id*='phone']" };
                foreach (var selector in phoneSelectors)
                {
                    try
                    {
                        var phoneElement = page.Locator(selector).First;
                        if (await phoneElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            venue.Phone = await phoneElement.TextContentAsync();
                            venue.Phone = venue.Phone?.Replace("Phone:", "").Trim();
                            if (!string.IsNullOrEmpty(venue.Phone)) break;
                        }
                    }
                    catch { }
                }

                // Website
                var websiteSelectors = new[] { "a[data-item-id='authority']", "a[aria-label*='Website']" };
                foreach (var selector in websiteSelectors)
                {
                    try
                    {
                        var websiteElement = page.Locator(selector).First;
                        if (await websiteElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            venue.Website = await websiteElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(venue.Website)) break;
                        }
                    }
                    catch { }
                }

                // Opening hours
                try
                {
                    var hoursElement = page.Locator("div[aria-label*='hours'], button[aria-label*='hours']").First;
                    if (await hoursElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                    {
                        venue.OpeningHours = await hoursElement.GetAttributeAsync("aria-label");
                    }
                }
                catch { }

                // Coordinates from URL
                var currentUrl = page.Url;
                var coordMatch = Regex.Match(currentUrl, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
                if (coordMatch.Success)
                {
                    venue.Latitude = double.Parse(coordMatch.Groups[1].Value);
                    venue.Longitude = double.Parse(coordMatch.Groups[2].Value);
                }

                // Main image
                var imageSelectors = new[] { "img.loaded", "img[decoding='async']", "img.YQ4gaf" };
                foreach (var selector in imageSelectors)
                {
                    try
                    {
                        var imageElement = page.Locator(selector).First;
                        if (await imageElement.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            venue.ImageUrl = await imageElement.GetAttributeAsync("src");
                            if (!string.IsNullOrEmpty(venue.ImageUrl) && venue.ImageUrl.StartsWith("http")) break;
                        }
                    }
                    catch { }
                }

                // Skip reviews extraction for now to speed up debugging
                // venue.Reviews = await ExtractReviewsAsync(page);

                return venue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting venue data");
                return null;
            }
        }

        private async Task<List<CrawledReviewData>> ExtractReviewsAsync(IPage page)
        {
            var reviews = new List<CrawledReviewData>();

            try
            {
                // Click on reviews tab
                var reviewsTab = page.Locator("button[aria-label*='Reviews']").First;
                if (await reviewsTab.IsVisibleAsync())
                {
                    await reviewsTab.ClickAsync();
                    await page.WaitForTimeoutAsync(2000);

                    // Scroll to load reviews
                    var reviewsFeed = page.Locator("div.m6QErb.DxyBCb.kA9KIf.dS8AEf").First;
                    if (await reviewsFeed.IsVisibleAsync())
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            await page.EvaluateAsync("el => el.scrollBy(0, 500)", await reviewsFeed.ElementHandleAsync());
                            await page.WaitForTimeoutAsync(1000);
                        }
                    }

                    // Extract review data
                    var reviewElements = await page.Locator("div.jftiEf").AllAsync();
                    foreach (var reviewEl in reviewElements.Take(10))
                    {
                        try
                        {
                            var review = new CrawledReviewData();

                            // Author
                            var authorEl = reviewEl.Locator("div.d4r55").First;
                            if (await authorEl.IsVisibleAsync())
                            {
                                review.Author = await authorEl.TextContentAsync() ?? "Anonymous";
                            }

                            // Rating
                            var ratingEl = reviewEl.Locator("span.kvMYJc").First;
                            if (await ratingEl.IsVisibleAsync())
                            {
                                var ariaLabel = await ratingEl.GetAttributeAsync("aria-label");
                                var match = Regex.Match(ariaLabel ?? "", @"(\d)");
                                if (match.Success)
                                {
                                    review.Rating = double.Parse(match.Groups[1].Value);
                                }
                            }

                            // Review text
                            var textEl = reviewEl.Locator("span.wiI7pd").First;
                            if (await textEl.IsVisibleAsync())
                            {
                                review.ReviewText = await textEl.TextContentAsync() ?? string.Empty;
                            }

                            // Date
                            var dateEl = reviewEl.Locator("span.rsqaWe").First;
                            if (await dateEl.IsVisibleAsync())
                            {
                                var dateText = await dateEl.TextContentAsync();
                                review.ReviewDate = ParseRelativeDate(dateText);
                            }

                            if (!string.IsNullOrEmpty(review.Author) && review.Rating > 0)
                            {
                                reviews.Add(review);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting reviews");
            }

            return reviews;
        }

        private async Task HandleGoogleConsentAsync(IPage page)
        {
            try
            {
                // Try various consent button selectors
                var consentSelectors = new[]
                {
                    "button:has-text('Accept all')",
                    "button:has-text('Accept All')",
                    "button:has-text('I agree')",
                    "button:has-text('Agree')",
                    "form[action*='consent'] button",
                    "button[aria-label*='Accept']",
                    "[aria-label='Accept all']",
                    "button.tHlp8d",  // Google's consent button class
                    "#L2AGLb"  // Common Google consent button ID
                };

                foreach (var selector in consentSelectors)
                {
                    try
                    {
                        var consentButton = page.Locator(selector).First;
                        if (await consentButton.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1000 }))
                        {
                            _logger.LogInformation("   🍪 Found consent button, clicking: {Selector}", selector);
                            await consentButton.ClickAsync();
                            await page.WaitForTimeoutAsync(2000);
                            _logger.LogInformation("   ✓ Consent dialog dismissed");
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("   No consent dialog found or error: {Message}", ex.Message);
            }
        }

        private bool IsLikelyEscapeRoom(string text)
        {
            var keywords = new[]
            {
                "escape room", "escape game", "room escape", "puzzle room",
                "exit game", "breakout", "escape experience", "live escape",
                "mystery room", "adventure room", "locked room"
            };

            return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private DateTime? ParseRelativeDate(string? dateText)
        {
            if (string.IsNullOrEmpty(dateText)) return null;

            dateText = dateText.ToLower();

            if (dateText.Contains("day"))
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (match.Success && int.TryParse(match.Value, out int days))
                {
                    return DateTime.UtcNow.AddDays(-days);
                }
            }
            else if (dateText.Contains("week"))
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (match.Success && int.TryParse(match.Value, out int weeks))
                {
                    return DateTime.UtcNow.AddDays(-weeks * 7);
                }
            }
            else if (dateText.Contains("month"))
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (match.Success && int.TryParse(match.Value, out int months))
                {
                    return DateTime.UtcNow.AddMonths(-months);
                }
            }
            else if (dateText.Contains("year"))
            {
                var match = Regex.Match(dateText, @"(\d+)");
                if (match.Success && int.TryParse(match.Value, out int years))
                {
                    return DateTime.UtcNow.AddYears(-years);
                }
            }

            return null;
        }

        private async Task<string?> DownloadImageAsync(string imageUrl, string venueName)
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(imageUrl);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"{GenerateSlug(venueName)}-{DateTime.UtcNow.Ticks}.jpg";
                    var relativePath = Path.Combine("images", "venues", fileName);
                    var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    await File.WriteAllBytesAsync(fullPath, imageBytes);

                    return "/" + relativePath.Replace("\\", "/");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image from {Url}", imageUrl);
            }

            return null;
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug.Trim('-');
        }

        public async Task SaveCrawledReviewsAsync(int venueId, List<CrawledReviewData> crawledReviews)
        {
            var reviews = ConvertToReviews(venueId, crawledReviews);

            foreach (var review in reviews)
            {
                // Check if review already exists (by content similarity)
                var exists = await _context.Reviews
                    .AnyAsync(r => r.VenueId == venueId &&
                                   r.Content == review.Content &&
                                   r.Rating == review.Rating);

                if (!exists)
                {
                    _context.Reviews.Add(review);
                }
            }

            await _context.SaveChangesAsync();
        }

        public List<Review> ConvertToReviews(int venueId, List<CrawledReviewData> crawledReviews, int? systemUserId = null)
        {
            return crawledReviews.Select(cr => new Review
            {
                VenueId = venueId,
                UserId = systemUserId,
                Rating = (int)Math.Round(cr.Rating),
                Title = cr.Author,
                Content = cr.ReviewText,
                VisitDate = cr.ReviewDate,
                IsVerifiedVisit = false,
                IsApproved = true, // Auto-approve crawled reviews
                HelpfulCount = cr.HelpfulCount ?? 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
        }
    }
}
