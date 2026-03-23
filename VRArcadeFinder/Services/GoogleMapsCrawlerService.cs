using Microsoft.Playwright;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Data;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace VRArcadeFinder.Services
{
    public class CrawlResult
    {
        public int Found { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
    }

    public interface IGoogleMapsCrawlerService
    {
        Task<CrawlResult> CrawlArcadesAsync(string searchQuery, int maxResults = 20);
        Task<Arcade?> CrawlSingleArcadeAsync(string placeId);
        Task<List<CrawledArcadeData>> CrawlWithPlaywrightAsync(string location, int maxResults = 20);
        Task<List<CrawledArcadeData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10);
    }

    public class CrawledReviewData
    {
        public string Author { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public DateTime? ReviewDate { get; set; }
        public int? HelpfulCount { get; set; }
    }

    public class CrawledArcadeData
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
        public List<CrawledReviewData> Reviews { get; set; } = new();
    }

    public class GoogleMapsCrawlerService : IGoogleMapsCrawlerService
    {
        private readonly ILogger<GoogleMapsCrawlerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHostEnvironment _environment;
        private readonly IImageStorageService _imageStorageService;

        public GoogleMapsCrawlerService(
            ILogger<GoogleMapsCrawlerService> logger,
            IServiceProvider serviceProvider,
            IWebHostEnvironment environment,
            IImageStorageService imageStorageService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _environment = environment;
            _imageStorageService = imageStorageService;
        }

        public async Task<CrawlResult> CrawlArcadesAsync(string searchQuery, int maxResults = 20)
        {
            var result = new CrawlResult();

            try
            {
                // Use Playwright to crawl
                var crawledData = await CrawlWithPlaywrightAsync(searchQuery, maxResults);
                result.Found = crawledData.Count;

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                foreach (var data in crawledData)
                {
                    try
                    {
                        // Check if arcade already exists by GooglePlaceId or Name+Address
                        var existingArcade = await context.Arcades
                            .FirstOrDefaultAsync(c =>
                                (!string.IsNullOrEmpty(c.GooglePlaceId) && c.GooglePlaceId == data.GooglePlaceId) ||
                                (c.Name == data.Name && c.Address == data.Address));

                        if (existingArcade != null)
                        {
                            // Update existing arcade
                            UpdateArcadeFromCrawledData(existingArcade, data);
                            context.Arcades.Update(existingArcade);
                            result.Updated++;
                        }
                        else
                        {
                            // Create new arcade
                            var arcade = CreateArcadeFromCrawledData(data);
                            if (arcade != null)
                            {
                                context.Arcades.Add(arcade);
                                result.Added++;
                            }
                        }

                        await context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing crawled arcade: {Name}", data.Name);
                    }
                }

                _logger.LogInformation("Crawl completed: Found={Found}, Added={Added}, Updated={Updated}",
                    result.Found, result.Added, result.Updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling arcades for query: {Query}", searchQuery);
                throw;
            }

            return result;
        }

        public async Task<Arcade?> CrawlSingleArcadeAsync(string placeId)
        {
            _logger.LogWarning("CrawlSingleArcadeAsync not implemented for Playwright");
            return null;
        }

        public async Task<List<CrawledArcadeData>> CrawlWithPlaywrightAsync(string location, int maxResults = 20)
        {
            var queries = new[]
            {
                $"VR arcade in {location}",
                $"virtual reality arcade {location}",
                $"VR gaming center {location}"
            };

            return await CrawlWithMultipleQueriesAsync(location, queries, maxResults / queries.Length + 1);
        }

        public async Task<List<CrawledArcadeData>> CrawlWithMultipleQueriesAsync(string location, string[] queries, int maxResultsPerQuery = 10)
        {
            var allResults = new List<CrawledArcadeData>();
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
                    var debugPath = Path.Combine(_environment.WebRootPath, "debug");
                    Directory.CreateDirectory(debugPath);
                    var screenshotPath = Path.Combine(debugPath, $"crawl-{DateTime.Now:yyyyMMdd-HHmmss}.png");
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
                            var arcadeName = ariaLabel ?? "";
                            _logger.LogInformation("   [{Index}] Found venue: {Name}", count + 1, arcadeName);

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

                            // Wait for details panel to load
                            try
                            {
                                await page.WaitForSelectorAsync("button[data-item-id='address'], div[data-attrid='kc:/location/location:address']", new PageWaitForSelectorOptions { Timeout = 5000 });
                            }
                            catch (TimeoutException)
                            {
                                _logger.LogWarning("   ⚠ Venue details panel didn't load");
                            }

                            var arcadeData = await ExtractArcadeDataAsync(page, arcadeName);
                            if (arcadeData != null && !string.IsNullOrEmpty(arcadeData.Name) && arcadeData.Name != "Results" && arcadeData.Name != "Sponsored?")
                            {
                                arcadeData.GooglePlaceId = placeId;
                                arcadeData.GoogleMapsUrl = href;

                                // Download image if available
                                if (!string.IsNullOrEmpty(arcadeData.ImageUrl))
                                {
                                    _logger.LogInformation("       📷 Downloading image...");
                                    arcadeData.LocalImagePath = await DownloadImageAsync(arcadeData.ImageUrl, arcadeData.Name);
                                }

                                allResults.Add(arcadeData);
                                count++;
                                _logger.LogInformation("   ✓ [{Index}] Extracted: {Name}", count, arcadeData.Name);
                                _logger.LogInformation("       📍 Address: {Address}", arcadeData.Address);
                                _logger.LogInformation("       ⭐ Rating: {Rating} ({Reviews} reviews)", arcadeData.Rating?.ToString("F1") ?? "N/A", arcadeData.ReviewCount ?? 0);
                                if (!string.IsNullOrEmpty(arcadeData.Website))
                                    _logger.LogInformation("       🌐 Website: {Website}", arcadeData.Website);
                            }
                            else
                            {
                                _logger.LogInformation("   ✗ Venue filtered out (not a VR arcade or missing data)");
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
            _logger.LogInformation("🏁 CRAWL COMPLETE: Total arcades extracted: {Count}", allResults.Count);
            _logger.LogInformation("════════════════════════════════════════════════════════════");

            return allResults;
        }

        private async Task<CrawledArcadeData?> ExtractArcadeDataAsync(IPage page, string? preExtractedName = null)
        {
            try
            {
                var arcade = new CrawledArcadeData();

                // Use pre-extracted name if provided (from aria-label), otherwise try to extract from page
                if (!string.IsNullOrEmpty(preExtractedName))
                {
                    arcade.Name = preExtractedName;
                    _logger.LogDebug("       Using pre-extracted name: {Name}", arcade.Name);
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
                                if (!string.IsNullOrEmpty(extractedName) && extractedName != "Results" && extractedName != "Sponsored?")
                                {
                                    arcade.Name = extractedName;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                _logger.LogDebug("       Final name: {Name}", arcade.Name);

                // Get category to check if it's a VR arcade
                var nameAndCategory = arcade.Name.ToLower();
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

                // Check if it's likely a VR arcade
                if (!IsLikelyVRArcade(nameAndCategory))
                {
                    _logger.LogDebug("       Not a VR arcade based on: {Text}", nameAndCategory);
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
                                arcade.Rating = rating;
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
                            arcade.ReviewCount = reviewCount;
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
                            arcade.Address = await addressElement.TextContentAsync() ?? string.Empty;
                            arcade.Address = arcade.Address.Replace("Address:", "").Trim();
                            if (!string.IsNullOrEmpty(arcade.Address))
                            {
                                _logger.LogDebug("       Address: {Address}", arcade.Address);
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
                            arcade.Phone = await phoneElement.TextContentAsync();
                            arcade.Phone = arcade.Phone?.Replace("Phone:", "").Trim();
                            if (!string.IsNullOrEmpty(arcade.Phone)) break;
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
                            arcade.Website = await websiteElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(arcade.Website)) break;
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
                        arcade.OpeningHours = await hoursElement.GetAttributeAsync("aria-label");
                    }
                }
                catch { }

                // Coordinates from URL
                var currentUrl = page.Url;
                var coordMatch = Regex.Match(currentUrl, @"@(-?\d+\.\d+),(-?\d+\.\d+)");
                if (coordMatch.Success)
                {
                    arcade.Latitude = double.Parse(coordMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    arcade.Longitude = double.Parse(coordMatch.Groups[2].Value, CultureInfo.InvariantCulture);
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
                            arcade.ImageUrl = await imageElement.GetAttributeAsync("src");
                            if (!string.IsNullOrEmpty(arcade.ImageUrl) && arcade.ImageUrl.StartsWith("http")) break;
                        }
                    }
                    catch { }
                }

                return arcade;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting arcade data");
                return null;
            }
        }

        private async Task HandleGoogleConsentAsync(IPage page)
        {
            try
            {
                var consentSelectors = new[]
                {
                    "button:has-text('Accept all')",
                    "button:has-text('Accept All')",
                    "button:has-text('I agree')",
                    "button:has-text('Agree')",
                    "form[action*='consent'] button",
                    "button[aria-label*='Accept']",
                    "[aria-label='Accept all']",
                    "button.tHlp8d",
                    "#L2AGLb"
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

        private bool IsLikelyVRArcade(string text)
        {
            var keywords = new[]
            {
                "vr", "virtual reality", "arcade", "gaming center", "game center",
                "vr experience", "vr gaming", "immersive", "simulator", "simulation",
                "entertainment center", "amusement", "play space", "gaming lounge"
            };

            return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private Arcade CreateArcadeFromCrawledData(CrawledArcadeData data)
        {
            var arcade = new Arcade
            {
                Name = data.Name,
                Address = data.Address,
                Phone = data.Phone,
                Website = data.Website,
                GooglePlaceId = data.GooglePlaceId,
                GoogleMapsUrl = data.GoogleMapsUrl,
                LocalImagePath = data.LocalImagePath,
                Slug = GenerateSlug(data.Name),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (data.Latitude.HasValue)
                arcade.Latitude = data.Latitude.Value;
            if (data.Longitude.HasValue)
                arcade.Longitude = data.Longitude.Value;
            if (data.Rating.HasValue)
                arcade.AverageRating = (decimal)data.Rating.Value;
            if (data.ReviewCount.HasValue)
                arcade.TotalReviews = data.ReviewCount.Value;

            // Parse address components
            ParseAddressFromString(arcade, data.Address);

            return arcade;
        }

        private void UpdateArcadeFromCrawledData(Arcade arcade, CrawledArcadeData data)
        {
            arcade.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(data.Phone) && string.IsNullOrEmpty(arcade.Phone))
                arcade.Phone = data.Phone;
            if (!string.IsNullOrEmpty(data.Website) && string.IsNullOrEmpty(arcade.Website))
                arcade.Website = data.Website;
            if (data.Rating.HasValue)
                arcade.AverageRating = (decimal)data.Rating.Value;
            if (data.ReviewCount.HasValue)
                arcade.TotalReviews = data.ReviewCount.Value;
            if (!string.IsNullOrEmpty(data.LocalImagePath) && string.IsNullOrEmpty(arcade.LocalImagePath))
                arcade.LocalImagePath = data.LocalImagePath;
            if (!string.IsNullOrEmpty(data.GoogleMapsUrl) && string.IsNullOrEmpty(arcade.GoogleMapsUrl))
                arcade.GoogleMapsUrl = data.GoogleMapsUrl;
        }

        private void ParseAddressFromString(Arcade arcade, string address)
        {
            var parts = address.Split(',').Select(p => p.Trim()).ToArray();

            if (parts.Length >= 2)
            {
                arcade.City = parts.Length >= 3 ? parts[^3] : parts[^2];
                arcade.Country = parts[^1];

                if (parts.Length >= 3)
                {
                    var stateZip = parts[^2].Trim();
                    var stateMatch = Regex.Match(stateZip, @"^([A-Z]{2})\s*\d*");
                    if (stateMatch.Success)
                    {
                        arcade.State = stateMatch.Groups[1].Value;
                        arcade.City = parts[^3];
                    }
                }
            }

            if (string.IsNullOrEmpty(arcade.City))
            {
                arcade.City = "Unknown";
            }

            if (string.IsNullOrEmpty(arcade.Country))
            {
                arcade.Country = "United States";
            }
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (slug.Length > 100)
            {
                slug = slug.Substring(0, 100).TrimEnd('-');
            }

            slug += "-" + Guid.NewGuid().ToString("N").Substring(0, 6);

            return slug;
        }

        private async Task<string?> DownloadImageAsync(string imageUrl, string arcadeName)
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(imageUrl);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"{GenerateSlug(arcadeName)}-{DateTime.UtcNow.Ticks}.jpg";
                    var relativePath = Path.Combine("images", "arcades", fileName);
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
    }
}
