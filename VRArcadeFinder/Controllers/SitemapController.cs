using VRArcadeFinder.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace VRArcadeFinder.Controllers
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
            var baseUrl = _configuration["SiteUrl"] ?? "https://vrarcadefinder.com";
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // Static pages
            sb.AppendLine($"  <url><loc>{baseUrl}/</loc><changefreq>daily</changefreq><priority>1.0</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/map</loc><changefreq>weekly</changefreq><priority>0.9</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/blog</loc><changefreq>weekly</changefreq><priority>0.8</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/games</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/listing/pricing</loc><changefreq>monthly</changefreq><priority>0.6</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/about</loc><changefreq>monthly</changefreq><priority>0.5</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/privacy</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>");
            sb.AppendLine($"  <url><loc>{baseUrl}/terms</loc><changefreq>monthly</changefreq><priority>0.3</priority></url>");

            // Arcades
            var arcades = await _context.Arcades
                .Where(a => a.IsActive)
                .Select(a => new { a.Slug, a.UpdatedAt })
                .ToListAsync();

            foreach (var arcade in arcades)
            {
                var lastMod = arcade.UpdatedAt.ToString("yyyy-MM-dd");
                sb.AppendLine($"  <url><loc>{baseUrl}/arcade/{arcade.Slug}</loc><lastmod>{lastMod}</lastmod><changefreq>weekly</changefreq><priority>0.8</priority></url>");
            }

            // VR Games
            var games = await _context.VRGames
                .Where(g => !string.IsNullOrEmpty(g.Slug))
                .Select(g => new { g.Slug, g.CreatedAt })
                .ToListAsync();

            foreach (var game in games)
            {
                var lastMod = game.CreatedAt.ToString("yyyy-MM-dd");
                sb.AppendLine($"  <url><loc>{baseUrl}/games/{game.Slug}</loc><lastmod>{lastMod}</lastmod><changefreq>monthly</changefreq><priority>0.6</priority></url>");
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

            // City pages (if implemented)
            var cities = await _context.Arcades
                .Where(a => a.IsActive && !string.IsNullOrEmpty(a.City))
                .Select(a => a.City)
                .Distinct()
                .ToListAsync();

            foreach (var city in cities)
            {
                var citySlug = city.ToLower().Replace(" ", "-");
                sb.AppendLine($"  <url><loc>{baseUrl}/?city={Uri.EscapeDataString(city)}</loc><changefreq>weekly</changefreq><priority>0.7</priority></url>");
            }

            sb.AppendLine("</urlset>");

            return Content(sb.ToString(), "application/xml");
        }

        [Route("robots.txt")]
        public IActionResult Robots()
        {
            var baseUrl = _configuration["SiteUrl"] ?? "https://vrarcadefinder.com";

            var content = $@"User-agent: *
Allow: /

Disallow: /account/
Disallow: /admin/
Disallow: /api/

Sitemap: {baseUrl}/sitemap.xml
";

            return Content(content, "text/plain");
        }
    }
}
