using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;

namespace CookingClassFinder.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAutoCrawlService _autoCrawlService;

    public AdminController(ApplicationDbContext context, IAutoCrawlService autoCrawlService)
    {
        _context = context;
        _autoCrawlService = autoCrawlService;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> Dashboard()
    {
        ViewBag.TotalSchools = await _context.Schools.CountAsync();
        ViewBag.TotalClasses = await _context.Classes.CountAsync();
        ViewBag.TotalReviews = await _context.Reviews.CountAsync();
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.PendingReviews = await _context.Reviews.CountAsync(r => !r.IsApproved);
        ViewBag.RecentSchools = await _context.Schools
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View();
    }

    public async Task<IActionResult> Schools()
    {
        var schools = await _context.Schools
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
        return View(schools);
    }

    public async Task<IActionResult> Classes()
    {
        var classes = await _context.Classes
            .Include(c => c.School)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return View(classes);
    }

    public async Task<IActionResult> CreateClass()
    {
        ViewBag.Schools = await _context.Schools
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateClass(CookingClass model)
    {
        if (ModelState.IsValid)
        {
            model.Slug = GenerateSlug(model.Name);
            model.CreatedAt = DateTime.UtcNow;
            _context.Classes.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Class created successfully.";
            return RedirectToAction(nameof(Classes));
        }

        ViewBag.Schools = await _context.Schools.Where(s => s.IsActive).ToListAsync();
        return View(model);
    }

    public async Task<IActionResult> Reviews(string? status)
    {
        var query = _context.Reviews
            .Include(r => r.School)
            .Include(r => r.User)
            .AsQueryable();

        if (status == "pending")
            query = query.Where(r => !r.IsApproved);
        else if (status == "approved")
            query = query.Where(r => r.IsApproved);

        ViewBag.Status = status;
        var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(reviews);
    }

    public async Task<IActionResult> ApproveReview(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review != null)
        {
            review.IsApproved = true;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Review approved.";
        }
        return RedirectToAction(nameof(Reviews));
    }

    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _context.Reviews.FindAsync(id);
        if (review != null)
        {
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Review deleted.";
        }
        return RedirectToAction(nameof(Reviews));
    }

    public async Task<IActionResult> BlogPosts()
    {
        var posts = await _context.BlogPosts
            .Include(p => p.Author)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(posts);
    }

    #region City & Crawl Management

    public async Task<IActionResult> Cities(string? filter, string? region, string? search, int page = 1)
    {
        var query = _context.Cities.AsQueryable();

        // Apply filters
        if (filter == "never")
            query = query.Where(c => c.CrawlCount == 0);
        else if (filter == "failed")
            query = query.Where(c => c.LastCrawlStatus == "Failed");
        else if (filter == "pending")
            query = query.Where(c => c.NextCrawlAt != null && c.NextCrawlAt <= DateTime.UtcNow);
        else if (filter == "inactive")
            query = query.Where(c => !c.IsActive);

        if (region == "US")
            query = query.Where(c => c.Region == "US");
        else if (region == "International")
            query = query.Where(c => c.Region == "International");

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Name.Contains(search) || c.Country.Contains(search));

        // Stats
        ViewBag.TotalCount = await _context.Cities.CountAsync();
        ViewBag.NeverCrawledCount = await _context.Cities.CountAsync(c => c.CrawlCount == 0);
        ViewBag.FailedCount = await _context.Cities.CountAsync(c => c.LastCrawlStatus == "Failed");
        ViewBag.PendingCount = await _context.Cities.CountAsync(c => c.NextCrawlAt != null && c.NextCrawlAt <= DateTime.UtcNow);
        ViewBag.USCount = await _context.Cities.CountAsync(c => c.Region == "US");
        ViewBag.InternationalCount = await _context.Cities.CountAsync(c => c.Region == "International");
        ViewBag.IsAutoCrawlRunning = _autoCrawlService.IsRunning;

        ViewBag.Filter = filter;
        ViewBag.Region = region;
        ViewBag.Search = search;

        // Pagination
        int pageSize = 50;
        var totalItems = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.CurrentPage = page;

        var cities = await query
            .OrderBy(c => c.CrawlCount)
            .ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(cities);
    }

    public async Task<IActionResult> CrawlStatus()
    {
        var history = await _context.CrawlHistories
            .Include(h => h.City)
            .OrderByDescending(h => h.StartedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.TotalCities = await _context.Cities.CountAsync();
        ViewBag.CitiesCrawled = await _context.Cities.CountAsync(c => c.CrawlCount > 0);
        ViewBag.CitiesNeverCrawled = await _context.Cities.CountAsync(c => c.CrawlCount == 0);
        ViewBag.CitiesFailed = await _context.Cities.CountAsync(c => c.LastCrawlStatus == "Failed");
        ViewBag.TotalCrawls = await _context.CrawlHistories.CountAsync();
        ViewBag.SuccessfulCrawls = await _context.CrawlHistories.CountAsync(h => h.Status == "Success");
        ViewBag.FailedCrawls = await _context.CrawlHistories.CountAsync(h => h.Status == "Failed");
        ViewBag.TotalSchoolsFound = await _context.CrawlHistories.SumAsync(h => h.SchoolsFound);
        ViewBag.TotalSchoolsAdded = await _context.CrawlHistories.SumAsync(h => h.SchoolsAdded);
        ViewBag.IsAutoCrawlRunning = _autoCrawlService.IsRunning;
        ViewBag.NextCities = await _autoCrawlService.GetNextCitiesToCrawlAsync(5);

        return View(history);
    }

    [HttpPost]
    public async Task<IActionResult> CrawlCity(int id)
    {
        var city = await _context.Cities.FindAsync(id);
        if (city == null)
            return Json(new { success = false, message = "City not found" });

        var result = await _autoCrawlService.CrawlCityAsync(city);

        return Json(new
        {
            success = result.Success,
            message = result.Success
                ? $"Crawled {city.Name}: Found {result.SchoolsFound}, Added {result.SchoolsAdded}, Updated {result.SchoolsUpdated}"
                : $"Crawl failed for {city.Name}: {result.ErrorMessage}"
        });
    }

    [HttpPost]
    public async Task<IActionResult> BulkCrawlCities([FromBody] int[] cityIds)
    {
        if (cityIds == null || cityIds.Length == 0)
            return Json(new { success = false, message = "No cities selected" });

        int success = 0, failed = 0;

        foreach (var cityId in cityIds)
        {
            var city = await _context.Cities.FindAsync(cityId);
            if (city == null) continue;

            var result = await _autoCrawlService.CrawlCityAsync(city);
            if (result.Success) success++; else failed++;
        }

        return Json(new
        {
            success = true,
            message = $"Crawled {success} cities successfully, {failed} failed."
        });
    }

    [HttpPost]
    public async Task<IActionResult> AddCity(string name, string? country, string region, int maxResults = 15)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "City name is required.";
            return RedirectToAction(nameof(Cities));
        }

        var city = new City
        {
            Name = name,
            Country = country ?? "United States",
            Region = region,
            Slug = GenerateSlug(name),
            MaxResults = maxResults
        };

        _context.Cities.Add(city);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"City '{name}' added successfully.";
        return RedirectToAction(nameof(Cities));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCityActive(int id)
    {
        var city = await _context.Cities.FindAsync(id);
        if (city == null)
        {
            TempData["ErrorMessage"] = "City not found.";
            return RedirectToAction(nameof(Cities));
        }

        city.IsActive = !city.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = city.IsActive
            ? $"'{city.Name}' is now active for auto crawl."
            : $"'{city.Name}' is now excluded from auto crawl.";

        return RedirectToAction(nameof(Cities));
    }

    [HttpPost]
    public async Task<IActionResult> ResetCityCrawlStatus(int id)
    {
        var city = await _context.Cities.FindAsync(id);
        if (city == null)
        {
            TempData["ErrorMessage"] = "City not found.";
            return RedirectToAction(nameof(Cities));
        }

        city.NextCrawlAt = null;
        city.LastCrawlStatus = null;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Crawl status reset for '{city.Name}'.";
        return RedirectToAction(nameof(Cities));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCity(int id)
    {
        var city = await _context.Cities
            .Include(c => c.CrawlHistories)
            .FirstOrDefaultAsync(c => c.CityId == id);

        if (city == null)
        {
            TempData["ErrorMessage"] = "City not found.";
            return RedirectToAction(nameof(Cities));
        }

        var cityName = city.Name;
        _context.CrawlHistories.RemoveRange(city.CrawlHistories);
        _context.Cities.Remove(city);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"City '{cityName}' and its crawl history have been deleted.";
        return RedirectToAction(nameof(Cities));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCityMaxResults(int id, int maxResults)
    {
        var city = await _context.Cities.FindAsync(id);
        if (city == null)
            return Json(new { success = false, message = "City not found" });

        city.MaxResults = Math.Clamp(maxResults, 5, 50);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult StopAutoCrawl()
    {
        _autoCrawlService.Stop();
        TempData["SuccessMessage"] = "Auto crawl stop requested.";
        return RedirectToAction(nameof(CrawlStatus));
    }

    [HttpPost]
    public async Task<IActionResult> SeedCities()
    {
        await _autoCrawlService.SeedCitiesAsync();
        TempData["SuccessMessage"] = "Cities seeded successfully.";
        return RedirectToAction(nameof(Cities));
    }

    #endregion

    public async Task<IActionResult> Users()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
        return View(users);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");

        // Remove special characters
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

        return slug.Trim('-');
    }
}
