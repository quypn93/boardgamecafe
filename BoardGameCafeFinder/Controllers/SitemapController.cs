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
        /// Generate dynamic sitemap including all cafe pages
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

                // Static pages
                sitemapXml += CreateSitemapEntry($"{baseUrl}/", "2026-01-20", "daily", "1.0");
                sitemapXml += CreateSitemapEntry($"{baseUrl}/Map", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntry($"{baseUrl}/blog", "2026-01-20", "daily", "0.9");
                sitemapXml += CreateSitemapEntry($"{baseUrl}/Home/Privacy", "2026-01-20", "monthly", "0.5");

                // Dynamic cafe pages
                var cafes = await _context.Cafes
                    .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Slug))
                    .Select(c => new { c.Slug, c.UpdatedAt })
                    .OrderByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                foreach (var cafe in cafes)
                {
                    var lastMod = cafe.UpdatedAt.ToString("yyyy-MM-dd");
                    sitemapXml += CreateSitemapEntry($"{baseUrl}/cafe/{cafe.Slug}", lastMod, "weekly", "0.8");
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
                    sitemapXml += CreateSitemapEntry($"{baseUrl}/blog/{post.Slug}", lastMod, "weekly", "0.7");
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
                    sitemapXml += CreateSitemapEntry($"{baseUrl}/blog/{citySlug}", lastMod, "weekly", "0.7");
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

        private string CreateSitemapEntry(string url, string lastMod, string changeFreq, string priority)
        {
            return $"  <url>\r\n" +
                   $"    <loc>{System.Web.HttpUtility.HtmlEncode(url)}</loc>\r\n" +
                   $"    <lastmod>{lastMod}</lastmod>\r\n" +
                   $"    <changefreq>{changeFreq}</changefreq>\r\n" +
                   $"    <priority>{priority}</priority>\r\n" +
                   $"  </url>\r\n";
        }
    }
}
