using EscapeRoomFinder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EscapeRoomFinder.Controllers
{
    public class SitemapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public SitemapController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private static readonly string[] SupportedLanguages = { "en", "vi", "ja", "ko", "zh", "th", "es", "de" };

        [Route("sitemap.xml")]
        public async Task<IActionResult> Index()
        {
            var baseUrl = _configuration["SiteUrl"] ?? "https://escaperoomfinder.com";
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");

            // Static pages with hreflang
            AddUrlWithHreflang(sb, baseUrl, "/", "daily", "1.0");
            AddUrlWithHreflang(sb, baseUrl, "/map", "weekly", "0.9");
            AddUrlWithHreflang(sb, baseUrl, "/blog", "weekly", "0.8");
            AddUrlWithHreflang(sb, baseUrl, "/cities", "weekly", "0.7");
            AddUrlWithHreflang(sb, baseUrl, "/themes", "weekly", "0.7");

            // Venues
            var venues = await _context.Venues
                .Where(v => v.IsActive)
                .Select(v => new { v.Slug, v.UpdatedAt })
                .ToListAsync();

            foreach (var venue in venues)
            {
                var lastMod = venue.UpdatedAt.ToString("yyyy-MM-dd");
                AddUrlWithHreflang(sb, baseUrl, $"/venue/{venue.Slug}", "weekly", "0.8", lastMod);
            }

            // Rooms
            var rooms = await _context.Rooms
                .Include(r => r.Venue)
                .Where(r => r.IsActive && r.Venue.IsActive)
                .Select(r => new { VenueSlug = r.Venue.Slug, RoomSlug = r.Slug, r.UpdatedAt })
                .ToListAsync();

            foreach (var room in rooms)
            {
                var lastMod = room.UpdatedAt.ToString("yyyy-MM-dd");
                AddUrlWithHreflang(sb, baseUrl, $"/venue/{room.VenueSlug}/room/{room.RoomSlug}", "weekly", "0.7", lastMod);
            }

            // Blog posts
            var posts = await _context.BlogPosts
                .Where(p => p.IsPublished)
                .Select(p => new { p.Slug, p.PublishedAt, p.UpdatedAt })
                .ToListAsync();

            foreach (var post in posts)
            {
                var lastMod = (post.UpdatedAt ?? post.PublishedAt)?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                AddUrlWithHreflang(sb, baseUrl, $"/blog/{post.Slug}", "monthly", "0.6", lastMod);
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml");
        }

        [Route("robots.txt")]
        public IActionResult Robots()
        {
            var baseUrl = _configuration["SiteUrl"] ?? "https://escaperoomfinder.com";

            var content = $@"User-agent: *
Allow: /

Sitemap: {baseUrl}/sitemap.xml
";

            return Content(content, "text/plain");
        }

        private static void AddUrlWithHreflang(StringBuilder sb, string baseUrl, string path, string changefreq, string priority, string? lastmod = null)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{baseUrl}{path}</loc>");
            if (!string.IsNullOrEmpty(lastmod))
            {
                sb.AppendLine($"    <lastmod>{lastmod}</lastmod>");
            }
            sb.AppendLine($"    <changefreq>{changefreq}</changefreq>");
            sb.AppendLine($"    <priority>{priority}</priority>");

            // Add hreflang alternates for all supported languages
            foreach (var lang in SupportedLanguages)
            {
                sb.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"{lang}\" href=\"{baseUrl}{path}\" />");
            }
            sb.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{baseUrl}{path}\" />");

            sb.AppendLine("  </url>");
        }
    }
}
