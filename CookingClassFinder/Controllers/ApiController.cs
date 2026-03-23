using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CookingClassFinder.Data;
using CookingClassFinder.Services;

namespace CookingClassFinder.Controllers;

[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ISchoolService _schoolService;

    public ApiController(ApplicationDbContext context, ISchoolService schoolService)
    {
        _context = context;
        _schoolService = schoolService;
    }

    [HttpGet("schools/search")]
    public async Task<IActionResult> SearchSchools([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var schools = await _context.Schools
            .Where(s => s.IsActive && s.Name.Contains(q))
            .OrderBy(s => s.Name)
            .Take(10)
            .Select(s => new
            {
                id = s.SchoolId,
                name = s.Name,
                city = s.City,
                country = s.Country
            })
            .ToListAsync();

        return Ok(schools);
    }

    [HttpGet("schools/nearby")]
    public async Task<IActionResult> GetNearbySchools(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int radius = 25)
    {
        var schools = await _schoolService.GetNearbySchoolsAsync(lat, lng, radius);
        return Ok(schools);
    }

    [HttpGet("cuisines")]
    public async Task<IActionResult> GetCuisines()
    {
        var cuisines = await _context.Classes
            .Where(c => !string.IsNullOrEmpty(c.CuisineType))
            .GroupBy(c => c.CuisineType)
            .Select(g => new
            {
                name = g.Key,
                count = g.Count()
            })
            .OrderByDescending(c => c.count)
            .ToListAsync();

        return Ok(cuisines);
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities()
    {
        var cities = await _context.Cities
            .Where(c => c.SchoolCount > 0)
            .OrderByDescending(c => c.SchoolCount)
            .Take(50)
            .Select(c => new
            {
                name = c.Name,
                slug = c.Slug,
                country = c.Country,
                schoolCount = c.SchoolCount
            })
            .ToListAsync();

        return Ok(cities);
    }

    [HttpGet("schools/{id}")]
    public async Task<IActionResult> GetSchool(int id)
    {
        var school = await _context.Schools
            .Include(s => s.Classes)
            .FirstOrDefaultAsync(s => s.SchoolId == id && s.IsActive);

        if (school == null)
            return NotFound();

        return Ok(new
        {
            id = school.SchoolId,
            name = school.Name,
            slug = school.Slug,
            address = school.Address,
            city = school.City,
            country = school.Country,
            phone = school.Phone,
            email = school.Email,
            website = school.Website,
            description = school.Description,
            averageRating = school.AverageRating,
            totalReviews = school.TotalReviews,
            totalClasses = school.TotalClasses,
            latitude = school.Latitude,
            longitude = school.Longitude,
            classes = school.Classes.Select(c => new
            {
                id = c.ClassId,
                name = c.Name,
                slug = c.Slug,
                cuisineType = c.CuisineType,
                difficultyLevel = c.DifficultyLevel,
                pricePerPerson = c.PricePerPerson,
                durationMinutes = c.DurationMinutes
            })
        });
    }

    [HttpPost("affiliate/click")]
    public async Task<IActionResult> RecordAffiliateClick([FromBody] AffiliateClickRequest request)
    {
        var click = new Models.Domain.AffiliateClick
        {
            SchoolId = request.SchoolId,
            ClassId = request.ClassId,
            LinkType = request.ClickType ?? "website",
            DestinationUrl = request.DestinationUrl,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            ReferrerPage = Request.Headers.Referer.ToString(),
            ClickedAt = DateTime.UtcNow
        };

        _context.AffiliateClicks.Add(click);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}

public class AffiliateClickRequest
{
    public int? SchoolId { get; set; }
    public int? ClassId { get; set; }
    public string? ClickType { get; set; }
    public string? DestinationUrl { get; set; }
}
