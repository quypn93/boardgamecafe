using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;
using CookingClassFinder.Data;

namespace CookingClassFinder.Controllers;

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
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Index()
    {
        var baseUrl = _configuration["App:BaseUrl"] ?? "https://www.cookingclassfinder.com";

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var sitemap = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                // Static pages
                CreateUrlElement(ns, baseUrl, "/", "daily", "1.0"),
                CreateUrlElement(ns, baseUrl, "/Home/About", "monthly", "0.5"),
                CreateUrlElement(ns, baseUrl, "/Home/Privacy", "monthly", "0.3"),
                CreateUrlElement(ns, baseUrl, "/Home/Terms", "monthly", "0.3"),
                CreateUrlElement(ns, baseUrl, "/Blog", "daily", "0.8"),
                CreateUrlElement(ns, baseUrl, "/Blog/Cities", "weekly", "0.7"),
                CreateUrlElement(ns, baseUrl, "/School/Cuisines", "weekly", "0.7"),
                CreateUrlElement(ns, baseUrl, "/Map", "weekly", "0.6")
            )
        );

        // Add schools
        var schools = await _context.Schools
            .Where(s => s.IsActive)
            .Select(s => new { s.Slug, s.UpdatedAt })
            .ToListAsync();

        foreach (var school in schools)
        {
            sitemap.Root?.Add(CreateUrlElement(ns, baseUrl, $"/School/{school.Slug}", "weekly", "0.8", school.UpdatedAt));
        }

        // Add classes
        var classes = await _context.Classes
            .Where(c => c.School != null && c.School.IsActive)
            .Select(c => new { c.Slug, c.UpdatedAt })
            .ToListAsync();

        foreach (var item in classes)
        {
            sitemap.Root?.Add(CreateUrlElement(ns, baseUrl, $"/School/Class/{item.Slug}", "weekly", "0.7", item.UpdatedAt));
        }

        // Add blog posts
        var posts = await _context.BlogPosts
            .Where(p => p.IsPublished)
            .Select(p => new { p.Slug, p.UpdatedAt })
            .ToListAsync();

        foreach (var post in posts)
        {
            sitemap.Root?.Add(CreateUrlElement(ns, baseUrl, $"/Blog/{post.Slug}", "monthly", "0.6", post.UpdatedAt));
        }

        // Add city guides
        var cities = await _context.Cities
            .Where(c => c.SchoolCount > 0)
            .Select(c => new { c.Slug, c.UpdatedAt })
            .ToListAsync();

        foreach (var city in cities)
        {
            sitemap.Root?.Add(CreateUrlElement(ns, baseUrl, $"/Blog/CityGuide/{city.Slug}", "weekly", "0.7", city.UpdatedAt));
        }

        return Content(sitemap.ToString(), "application/xml", Encoding.UTF8);
    }

    private static XElement CreateUrlElement(XNamespace ns, string baseUrl, string path, string changefreq, string priority, DateTime? lastmod = null)
    {
        var element = new XElement(ns + "url",
            new XElement(ns + "loc", baseUrl + path),
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority)
        );

        if (lastmod.HasValue)
        {
            element.Add(new XElement(ns + "lastmod", lastmod.Value.ToString("yyyy-MM-dd")));
        }

        return element;
    }
}
