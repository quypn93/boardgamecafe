using BoardGameCafeFinder.Models.Domain;
using Microsoft.Playwright;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BoardGameCafeFinder.Services
{
    public interface ICafeWebsiteCrawlerService
    {
        Task<List<CrawledGameData>> CrawlCafeWebsiteForGamesAsync(string websiteUrl);
    }

    public class CrawledGameData
    {
        public string Name { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Description { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public int? PlaytimeMinutes { get; set; }
        public int? BggId { get; set; }
        public decimal? Price { get; set; }
    }

    // Internal class for JSON deserialization from Playwright
    internal class CrawledGameDataJson
    {
        public string? Name { get; set; }
        public string? SourceUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? PriceStr { get; set; }
    }

    public class CafeWebsiteCrawlerService : ICafeWebsiteCrawlerService
    {
        private readonly ILogger<CafeWebsiteCrawlerService> _logger;
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public CafeWebsiteCrawlerService(ILogger<CafeWebsiteCrawlerService> logger)
        {
            _logger = logger;
        }

        public async Task<List<CrawledGameData>> CrawlCafeWebsiteForGamesAsync(string websiteUrl)
        {
            var results = new List<CrawledGameData>();

            if (string.IsNullOrEmpty(websiteUrl))
                return results;

            try
            {
                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();

                // 1. Visit Main Page
                _logger.LogInformation("Visiting cafe website: {Url}", websiteUrl);
                await page.GotoAsync(websiteUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });

                // 2. Scan for "Games", "Library", "Menu" links
                var targetUrl = websiteUrl;
                var links = page.Locator("a");
                var count = await links.CountAsync();
                
                string? gamePageUrl = null;
                var keywords = new[] { "games", "collection", "library", "board games", "menu", "list", "shop", "shopping", "store", "buy" };

                for (int i = 0; i < count; i++)
                {
                    try {
                        var href = await links.Nth(i).GetAttributeAsync("href");
                        var text = await links.Nth(i).InnerTextAsync();

                        if (!string.IsNullOrEmpty(href) && !string.IsNullOrEmpty(text))
                        {
                            if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase) || href.Contains(k, StringComparison.OrdinalIgnoreCase)))
                            {
                                // Construct absolute URL
                                if (!href.StartsWith("http"))
                                {
                                    var uri = new Uri(new Uri(websiteUrl), href);
                                    gamePageUrl = uri.ToString();
                                }
                                else
                                {
                                    gamePageUrl = href;
                                }
                                break;
                            }
                        }
                    } catch {}
                }

                if (!string.IsNullOrEmpty(gamePageUrl))
                {
                     _logger.LogInformation("Found potential game list page: {Url}", gamePageUrl);
                     try 
                     {
                        await page.GotoAsync(gamePageUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.NetworkIdle });
                     }
                     catch 
                     {
                        // Fallback if NetworkIdle times out
                        await page.GotoAsync(gamePageUrl, new PageGotoOptions { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
                     }
                     targetUrl = gamePageUrl;
                }

                // 3. Extract Games from the page
                // Heuristic: Look for elements that might be game titles (lists, table rows, cards)

                var jsonString = await page.EvaluateAsync<string>(@"() => {
                    const results = [];
                    // Helper to clean text
                    const clean = (txt) => txt ? txt.trim().replace(/\s+/g, ' ') : '';

                    // Strategy 1: Explicit Product Links (common in Shopify/E-commerce)
                    const productLinks = document.querySelectorAll(""a[href*='/products/']"");
                    const processedUrls = new Set();

                    for (const link of productLinks) {
                        const href = link.href;
                        if (processedUrls.has(href)) continue;

                        // Try to find title
                        let title = clean(link.innerText);
                        let parent = link.parentElement;
                        let img = null;
                        let price = null;
                        let depth = 0;

                        // Scan up to find container
                        let container = link;
                        while(parent && depth < 5) {
                             // Look for better title if current is empty or generic 'read more'
                             if (!title || title.toLowerCase() === 'view' || title.toLowerCase() === 'shop') {
                                 const hTag = parent.querySelector('h1,h2,h3,h4,h5,.title,.product-title,.name');
                                 if (hTag) title = clean(hTag.innerText);
                             }

                             // Look for image
                             if (!img) {
                                 const imgEl = parent.querySelector('img');
                                 if (imgEl) {
                                     img = imgEl.src || imgEl.getAttribute('data-src') || imgEl.getAttribute('srcset');
                                     if (img && img.startsWith('//')) img = 'https:' + img;
                                 }
                             }

                             // Look for price
                             if (!price) {
                                  // 1. Look for specific price elements first
                                  const priceEl = parent.querySelector('.price, .money, product-price, span[class*=""price""]');
                                  let priceText = priceEl ? priceEl.innerText : parent.innerText;

                                  const currencyMatch = priceText.match(/(\$|€|£|₫|VND|vnd)\s*([\d,.]+)/i);
                                  if (currencyMatch) {
                                      price = currencyMatch[2].replace(/,/g, '').replace(/\./g, '');
                                  } else {
                                      // Fallback for just numbers if 'price' class was found
                                      const numMatch = priceText.match(/[\d,.]+/);
                                      if (priceEl && numMatch) {
                                            price = numMatch[0].replace(/,/g, ''); // Assume USD/std format if ambiguous or VND if large
                                      }
                                  }
                             }

                             container = parent;
                             parent = parent.parentElement;
                             depth++;
                        }

                        if (title && title.length > 2 && title.length < 100) {
                            results.push({
                                name: title,
                                imageUrl: img,
                                sourceUrl: href,
                                priceStr: price
                            });
                            processedUrls.add(href);
                        }
                    }

                    // Strategy 2: Generic Headings (fallback)
                    if (results.length === 0) {
                        const elements = document.querySelectorAll('h1, h2, h3, h4, strong, b, span.title, div.name');

                        for (const el of elements) {
                             const text = clean(el.innerText);
                             if (text.length > 2 && text.length < 50) {
                                 let img = null;
                                 let parent = el.parentElement;
                                 let depth = 0;
                                 while(parent && depth < 3) {
                                     const imgEl = parent.querySelector('img');
                                     if (imgEl && imgEl.src) { img = imgEl.src; break; }
                                     parent = parent.parentElement;
                                     depth++;
                                 }

                                 results.push({
                                     name: text,
                                     imageUrl: img,
                                     sourceUrl: window.location.href,
                                     priceStr: null
                                 });
                             }
                        }
                    }

                    return JSON.stringify(results);
                }");

                if (!string.IsNullOrEmpty(jsonString))
                {
                    var potentialElements = JsonSerializer.Deserialize<List<CrawledGameDataJson>>(jsonString, _jsonOptions);

                    if (potentialElements != null)
                    {
                        foreach (var item in potentialElements)
                        {
                            if (string.IsNullOrEmpty(item.Name))
                                continue;

                            // Skip if not a valid board game name
                            if (!IsLikelyBoardGameName(item.Name))
                                continue;

                            var crawledData = new CrawledGameData
                            {
                                Name = item.Name,
                                ImageUrl = item.ImageUrl,
                                SourceUrl = item.SourceUrl
                            };

                            // Parse Price
                            if (!string.IsNullOrEmpty(item.PriceStr))
                            {
                                if (decimal.TryParse(item.PriceStr, out decimal p))
                                    crawledData.Price = p;
                            }

                            results.Add(crawledData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crawling cafe website {Url}", websiteUrl);
            }

            return results.DistinctBy(x => x.Name).ToList();
        }

        /// <summary>
        /// Check if a string is likely a board game name vs website noise
        /// </summary>
        private static bool IsLikelyBoardGameName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var trimmed = name.Trim();

            // Too short or too long for a game name
            if (trimmed.Length < 3 || trimmed.Length > 100)
                return false;

            // Exclude website UI/navigation text
            var excludeExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "menu", "home", "contact", "about", "cart", "search", "login", "logout", "register",
                "shop", "store", "buy", "sell", "view", "more", "read more", "learn more", "view all",
                "subscribe", "newsletter", "sign up", "sign in", "follow", "share", "like",
                "facebook", "instagram", "twitter", "youtube", "tiktok", "pinterest",
                "privacy", "policy", "terms", "conditions", "faq", "help", "support",
                "checkout", "shipping", "returns", "refund", "wishlist", "favorites",
                "all products", "all games", "new arrivals", "best sellers", "on sale",
                "gift card", "gift certificate", "e-gift card",
                // Blog/CMS sections
                "recent posts", "recent comments", "archives", "categories", "tags",
                "leave a comment", "post comment", "comments", "sidebar",
                // Menu/Food sections
                "spring menu", "summer menu", "fall menu", "winter menu", "food menu",
                "drinks menu", "beverage menu", "snacks", "appetizers", "desserts",
                // Common website sections
                "navigation", "footer", "header", "copyright", "sitemap",
                "our story", "about us", "our team", "careers", "jobs",
                "blog", "news", "events", "gallery", "photos",
                // Vietnamese website UI text
                "giới thiệu", "trang", "trang chủ", "liên hệ", "ảnh", "hình ảnh", "video",
                "đăng nhập", "đăng ký", "đăng xuất", "tìm kiếm", "giỏ hàng", "thanh toán",
                "sản phẩm", "dịch vụ", "tin tức", "bài viết", "thông tin", "chi tiết",
                "xem thêm", "đọc thêm", "tất cả", "danh mục", "chuyên mục",
                "theo dõi", "chia sẻ", "thích", "bình luận", "đánh giá",
                "địa chỉ", "số điện thoại", "email", "hotline", "zalo",
                "chính sách", "điều khoản", "bảo mật", "hỗ trợ", "câu hỏi",
                "giá", "khuyến mãi", "giảm giá", "freeship", "miễn phí",
                "mua ngay", "thêm vào giỏ", "đặt hàng", "mua hàng",
                "bảng giá", "menu đồ uống", "thực đơn"
            };

            if (excludeExact.Contains(trimmed))
                return false;

            // Exclude patterns that indicate non-game content
            var excludePatterns = new[]
            {
                "currently unavailable", "out of stock", "sold out", "coming soon",
                "online store", "follow us", "subscribe to", "join our", "sign up for",
                "free shipping", "% off", "discount", "promo", "coupon",
                "copyright", "all rights reserved", "©",
                "click here", "tap here", "learn more", "read more",
                "mewsletter", "newsletter", "email us", "contact us", "call us",
                "social media", "connect with", "stay connected",
                "gift card", "e-gift", "voucher",
                "sleeve", "dice bag", "playmat", "card holder", "accessory",
                "t-shirt", "shirt", "mug", "poster", "merchandise",
                "add to cart", "buy now", "shop now", "view cart",
                "customer service", "track order", "my account",
                "powered by", "built with", "designed by",
                // Blog/CMS patterns
                "recent posts", "recent comments", "leave a reply", "post a comment",
                "no comments", "comments are closed", "tagged with", "filed under",
                "read more", "continue reading", "older posts", "newer posts",
                // Menu/seasonal patterns
                "spring menu", "summer menu", "fall menu", "winter menu", "seasonal menu",
                "food & drink", "our menu", "view menu",
                // Website section patterns
                "our location", "find us", "get directions", "hours of operation",
                "opening hours", "business hours", "we are open", "we are closed",
                "reservation", "book a table", "book now", "make a reservation"
            };

            var lowerName = trimmed.ToLowerInvariant();
            if (excludePatterns.Any(p => lowerName.Contains(p)))
                return false;

            // Exclude if it starts with special characters or numbers only
            if (trimmed.StartsWith("*") || trimmed.StartsWith("#") || trimmed.StartsWith("$"))
                return false;

            // Exclude if it's mostly punctuation or special characters
            var alphanumericCount = trimmed.Count(c => char.IsLetterOrDigit(c));
            if (alphanumericCount < trimmed.Length * 0.5)
                return false;

            // Exclude numeric strings like "4,2K", "1.5M", "100", "2024", etc.
            // Pattern: starts with digit, may contain separators and unit suffixes
            if (Regex.IsMatch(trimmed, @"^[\d.,\s]+(K|M|k|m|tr|triệu|nghìn|ngàn)?$"))
                return false;

            // Exclude strings that are mostly numbers (like follower counts, prices without context)
            var digitCount = trimmed.Count(char.IsDigit);
            var letterCount = trimmed.Count(char.IsLetter);
            if (digitCount > 0 && letterCount == 0)
                return false;
            if (digitCount > letterCount && trimmed.Length < 10)
                return false;

            // Exclude if it ends with common non-game suffixes
            var excludeSuffixes = new[] { "...", "!", "?", "→", "›", "»" };
            if (excludeSuffixes.Any(s => trimmed.EndsWith(s)))
            {
                // Allow if it's a known game pattern (some games have ! in name like "Uno!")
                // But filter out things like "Follow us!" or "Subscribe now!"
                var withoutSuffix = trimmed.TrimEnd('.', '!', '?', '→', '›', '»', ' ');
                if (withoutSuffix.Split(' ').Length <= 2 && excludePatterns.Any(p => withoutSuffix.ToLowerInvariant().Contains(p.Split(' ')[0])))
                    return false;
            }

            // Exclude sentences (likely descriptions, not titles)
            var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount > 10)
                return false;

            return true;
        }
    }
}
