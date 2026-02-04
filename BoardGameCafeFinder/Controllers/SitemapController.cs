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

        // Supported languages for SEO
        private static readonly string[] SupportedLanguages = { "en", "vi", "ja", "ko", "zh", "th", "es", "de" };

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
                sitemapXml += "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\r\n";

                // Static pages with hreflang
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/", "2026-01-20", "daily", "1.0");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/Map", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/blog", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntryWithHreflang(baseUrl, "/Home/Privacy", "2026-01-20", "monthly", "0.5");

                // Dynamic cafe pages with hreflang
                var cafes = await _context.Cafes
                    .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Slug))
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
                var cityPosts = await _context.Cafes
                    .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City))
                    .GroupBy(c => c.City)
                    .Select(g => new
                    {
                        City = g.Key,
                        LastUpdated = g.Max(c => c.UpdatedAt),
                        CafeCount = g.Count()
                    })
                    .Where(c => c.CafeCount > 0)
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
        /// Creates a sitemap entry with hreflang tags for all supported languages.
        /// This is important for SEO as it tells search engines about language alternatives.
        /// </summary>
        private string CreateSitemapEntryWithHreflang(string baseUrl, string path, string lastMod, string changeFreq, string priority)
        {
            var sb = new System.Text.StringBuilder();

            // For each supported language, create a URL entry with all hreflang alternatives
            foreach (var lang in SupportedLanguages)
            {
                var langPath = lang == "en" ? path : $"/{lang}{path}";
                var fullUrl = $"{baseUrl}{langPath}";

                sb.AppendLine("  <url>");
                sb.AppendLine($"    <loc>{System.Web.HttpUtility.HtmlEncode(fullUrl)}</loc>");
                sb.AppendLine($"    <lastmod>{lastMod}</lastmod>");
                sb.AppendLine($"    <changefreq>{changeFreq}</changefreq>");
                sb.AppendLine($"    <priority>{priority}</priority>");

                // Add hreflang links for all language versions
                foreach (var altLang in SupportedLanguages)
                {
                    var altPath = altLang == "en" ? path : $"/{altLang}{path}";
                    var altUrl = $"{baseUrl}{altPath}";
                    sb.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"{altLang}\" href=\"{System.Web.HttpUtility.HtmlEncode(altUrl)}\" />");
                }

                // Add x-default (pointing to English version)
                var defaultUrl = $"{baseUrl}{path}";
                sb.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{System.Web.HttpUtility.HtmlEncode(defaultUrl)}\" />");

                sb.AppendLine("  </url>");
            }

            return sb.ToString();
        }
    }
}
