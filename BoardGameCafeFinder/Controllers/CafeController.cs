using Microsoft.AspNetCore.Mvc;
using BoardGameCafeFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers;

[Route("cafe")]
public class CafeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CafeController> _logger;

    public CafeController(ApplicationDbContext context, ILogger<CafeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return NotFound();
        }

        var cafe = await _context.Cafes
            .Include(c => c.Photos)
            .Include(c => c.Reviews)
                .ThenInclude(r => r.User)
            .Include(c => c.CafeGames)
                .ThenInclude(cg => cg.Game)
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

        if (cafe == null)
        {
            // Try to find by ID if slug is numeric (backward compatibility)
            if (int.TryParse(slug, out int id))
            {
                cafe = await _context.Cafes
                    .Include(c => c.Photos)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.User)
                    .Include(c => c.CafeGames)
                        .ThenInclude(cg => cg.Game)
                    .FirstOrDefaultAsync(c => c.CafeId == id && c.IsActive);

                // Redirect to slug URL if found
                if (cafe != null && !string.IsNullOrEmpty(cafe.Slug))
                {
                    return RedirectToActionPermanent(nameof(Details), new { slug = cafe.Slug });
                }
            }

            if (cafe == null)
            {
                return NotFound();
            }
        }

        // Set SEO metadata for cafe detail page
        ViewData["Title"] = cafe.Name;

        var description = $"{cafe.Name} - Board game cafe";
        if (!string.IsNullOrEmpty(cafe.City))
        {
            description += $" in {cafe.City}";
        }
        if (cafe.AverageRating.HasValue && cafe.AverageRating > 0)
        {
            description += $" with {cafe.AverageRating:0.0} star rating";
        }
        if (!string.IsNullOrEmpty(cafe.Description))
        {
            description = $"{cafe.Name} - {cafe.Description}".Substring(0, Math.Min(160, $"{cafe.Name} - {cafe.Description}".Length));
        }

        ViewData["MetaDescription"] = description;
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/cafe/{cafe.Slug}";

        return View(cafe);
    }
}
