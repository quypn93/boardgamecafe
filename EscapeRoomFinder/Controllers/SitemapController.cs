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

        [Route("sitemap.xml")]
        public async Task<IActionResult> Index()
        {
            var baseUrl = _configuration["SiteUrl"] ?? "https://escaperoomfinder.com";
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Static pages
            sb.AppendLine($"  <url><loc>{baseUrl}/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/map</loc><changefreq>weekly</changefreq><priority>0.9</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/blog</loc><changefreq>weekly</changefreq><priority>0.8</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/cities</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/themes</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/about</loc><changefreq>monthly</changefreq><priority>0.5</priority></url>");

            // Venues
            var venues = await _context.Venues
                .Where(v => v.IsActive)
                .Select(v => new { v.Slug, v.UpdatedAt })
                .ToListAsync();

            foreach (var venue in venues)
            {
                var lastMod = venue.UpdatedAt.ToString("yyyy-MM-dd");
                sb.AppendLine($"  <url><loc>{baseUrl}/venue/{venue.Slug}</loc><lastmod>{lastMod}</lastmod><changefreq>weekly</changefreq><priority>0.8</priority></url>");
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
                sb.AppendLine($"  <url><loc>{baseUrl}/venue/{room.VenueSlug}/room/{room.RoomSlug}</loc><lastmod>{lastMod}</lastmod><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            }

            // Blog posts
            var posts = await _context.BlogPosts
                .Where(p => p.IsPublished)
                .Select(p => new { p.Slug, p.PublishedAt, p.UpdatedAt })
                .ToListAsync();

            foreach (var post in posts)
            {
                var lastMod = (post.UpdatedAt ?? post.PublishedAt)?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                sb.AppendLine($"  <url><loc>{baseUrl}/blog/{post.Slug}</loc><lastmod>{lastMod}</lastmod><changefreq>monthly</changefreq><priority>0.6</priority></url>");
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
    }
}
