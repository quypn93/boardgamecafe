using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardGameCafeFinder.Data;

namespace BoardGameCafeFinder.Controllers
{
    [Route("")]
    public class SitemapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SitemapController> _logger;

        public SitemapController(ApplicationDbContext context, ILogger<SitemapController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Generate dynamic sitemap including all cafe pages with hreflang for multi-language SEO
        /// GET: /sitemap.xml
        /// </summary>
        [HttpGet("sitemap.xml")]
        [Produces("application/xml")]
        public async Task<IActionResult> Sitemap()
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var sitemapXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n";
                sitemapXml += "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\r\n";

                // Static pages with hreflang
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/", "2026-01-20", "daily", "1.0");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/Map", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/blog", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/Home/Privacy", "2026-01-20", "monthly", "0.5");

                // Dynamic cafe pages with hreflang
                // Exclude cafes with fewer than 3 reviews to avoid thin content in sitemap
                var cafes = await _context.Cafes
                    .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Slug) && c.TotalReviews >= 10)
                    .Select(c => new { c.Slug, c.UpdatedAt })
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                foreach (var cafe in cafes)
                {
                    var lastMod = cafe.UpdatedAt.ToString("yyyy-MM-dd");
                    sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, $"/cafe/{cafe.Slug}", lastMod, "weekly", "0.8");
                }

                // Dynamic blog post pages from BlogPosts table
                var blogPosts = await _context.BlogPosts
                    .Where(p => p.IsPublished && !string.IsNullOrEmpty(p.Slug))
                    .Select(p => new { p.Slug, p.UpdatedAt, p.PublishedAt })
                    .OrderByDescending(p => p.PublishedAt)
                    .ToListAsync();

                foreach (var post in blogPosts)
                {
                    var lastMod = (post.UpdatedAt ?? post.PublishedAt ?? DateTime.Now).ToString("yyyy-MM-dd");
                    sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, $"/blog/{post.Slug}", lastMod, "weekly", "0.7");
                }

                // Dynamic city blog posts (generated from Cafes table - not in BlogPosts)
                // Only include cities with 3+ cafes to avoid thin content in sitemap
                var cityPosts = await _context.Cafes
                    .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City))
                    .GroupBy(c => c.City)
                    .Select(g => new
                    {
                        City = g.Key,
                        LastUpdated = g.Max(c => c.UpdatedAt),
                        CafeCount = g.Count()
                    })
                    .Where(c => c.CafeCount >= 3)
                    .OrderByDescending(c => c.CafeCount)
                    .ToListAsync();

                foreach (var cityPost in cityPosts)
                {
                    var citySlug = cityPost.City.ToLower().Replace(" ", "-");
                    var lastMod = cityPost.LastUpdated.ToString("yyyy-MM-dd");
                    sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, $"/blog/{citySlug}", lastMod, "weekly", "0.7");
                }

                sitemapXml += "</urlset>";

                return Content(sitemapXml, "application/xml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sitemap");
                return StatusCode(500, "Error generating sitemap");
            }
        }

        /// <summary>
        /// robots.txt file
        /// GET: /robots.txt
        /// </summary>
        [HttpGet("robots.txt")]
        [Produces("text/plain")]
        public IActionResult RobotsTxt()
        {
            var robotsTxt = "User-agent: *\r\n";
            robotsTxt += "Allow: /\r\n";
            robotsTxt += "\r\n";
            robotsTxt += "# Disallow crawling of admin and test pages\r\n";
            robotsTxt += "Disallow: /Admin/\r\n";
            robotsTxt += "Disallow: /Test/\r\n";
            robotsTxt += "Disallow: /api/\r\n";
            robotsTxt += "\r\n";
            robotsTxt += "# Disallow paginated and filtered pages to save crawl budget\r\n";
            robotsTxt += "Disallow: /*?*page=\r\n";
            robotsTxt += "Disallow: /*?*lat=\r\n";
            robotsTxt += "Disallow: /*?*lng=\r\n";
            robotsTxt += "Disallow: /*?*gameIds=\r\n";
            robotsTxt += "Disallow: /*?*categories=\r\n";
            robotsTxt += "\r\n";
            robotsTxt += "# Block language-prefixed URLs (content served via English URLs)\r\n";
            robotsTxt += "Disallow: /vi/\r\n";
            robotsTxt += "Disallow: /ja/\r\n";
            robotsTxt += "Disallow: /ko/\r\n";
            robotsTxt += "Disallow: /zh/\r\n";
            robotsTxt += "Disallow: /th/\r\n";
            robotsTxt += "Disallow: /es/\r\n";
            robotsTxt += "Disallow: /de/\r\n";
            robotsTxt += "\r\n";
            robotsTxt += "# Allow crawling of important pages\r\n";
            robotsTxt += "Allow: /\r\n";
            robotsTxt += "Allow: /cafe/\r\n";
            robotsTxt += "Allow: /blog/\r\n";
            robotsTxt += "Allow: /Map/\r\n";
            robotsTxt += "\r\n";
            robotsTxt += "# Sitemap location\r\n";
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            robotsTxt += $"Sitemap: {baseUrl}/sitemap.xml\r\n";

            return Content(robotsTxt, "text/plain");
        }

        /// <summary>
        /// Creates a single sitemap entry (English URL only) with hreflang tags pointing to all language versions.
        /// Hreflang in HTML head (via _HreflangTags.cshtml) already handles language discovery for search engines.
        /// Only English URLs in sitemap to reduce crawl budget waste from thin multilingual duplicates.
        /// </summary>
        private string CreateSitemapEntryWithHreflang(string baseUrl, string path, string lastMod, string changeFreq, string priority)
        {
            var sb = new System.Text.StringBuilder();

            var fullUrl = $"{baseUrl}{path}";

            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{System.Web.HttpUtility.HtmlEncode(fullUrl)}</loc>");
            sb.AppendLine($"    <lastmod>{lastMod}</lastmod>");
            sb.AppendLine($"    <changefreq>{changeFreq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");
            sb.AppendLine("  </url>");

            return sb.ToString();
        }
    }
}
