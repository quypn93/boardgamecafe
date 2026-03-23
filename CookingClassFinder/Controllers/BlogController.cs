using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CookingClassFinder.Data;
using CookingClassFinder.Services;
using CookingClassFinder.Models.ViewModels;

namespace CookingClassFinder.Controllers;

public class BlogController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBlogService _blogService;

    public BlogController(ApplicationDbContext context, IBlogService blogService)
    {
        _context = context;
        _blogService = blogService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var posts = await _context.BlogPosts
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .Skip((page - 1) * 10)
            .Take(10)
            .ToListAsync();

        return View(posts);
    }

    public async Task<IActionResult> Post(string slug)
    {
        var post = await _context.BlogPosts
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);

        if (post == null)
            return NotFound();

        // Increment view count
        post.ViewCount++;
        await _context.SaveChangesAsync();

        return View(post);
    }

    public async Task<IActionResult> Cities()
    {
        var cities = await _context.Cities
            .Where(c => c.SchoolCount > 0)
            .OrderByDescending(c => c.SchoolCount)
            .Select(c => new CityBlogItem
            {
                Name = c.Name,
                Slug = c.Slug,
                Country = c.Country,
                ImageUrl = c.ImageUrl,
                SchoolCount = c.SchoolCount
            })
            .ToListAsync();

        return View(cities);
    }

    public async Task<IActionResult> CityGuide(string slug)
    {
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Slug == slug);

        if (city == null)
            return NotFound();

        ViewBag.Schools = await _context.Schools
            .Where(s => s.City == city.Name && s.IsActive)
            .OrderByDescending(s => s.AverageRating)
            .Take(20)
            .ToListAsync();

        return View(city);
    }
}
