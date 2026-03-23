using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CookingClassFinder.Data;
using CookingClassFinder.Models.DTOs;

namespace CookingClassFinder.Controllers;

public class MapController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public MapController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(double? lat, double? lng, string? city, string? cuisine)
    {
        var query = _context.Schools
            .Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(city))
        {
            query = query.Where(s => s.City == city);
        }

        if (!string.IsNullOrEmpty(cuisine))
        {
            query = query.Where(s => s.CuisineSpecialties != null &&
                s.CuisineSpecialties.Contains(cuisine));
        }

        var schools = await query
            .OrderByDescending(s => s.AverageRating)
            .Take(100)
            .Select(s => new MapSchoolDto
            {
                SchoolId = s.SchoolId,
                Name = s.Name,
                Slug = s.Slug,
                Address = s.Address,
                City = s.City,
                Lat = s.Latitude,
                Lng = s.Longitude,
                Rating = s.AverageRating,
                ClassCount = s.TotalClasses
            })
            .ToListAsync();

        var model = new MapViewModel
        {
            Schools = schools,
            CenterLat = lat ?? schools.FirstOrDefault()?.Lat ?? 40.7128,
            CenterLng = lng ?? schools.FirstOrDefault()?.Lng ?? -74.0060,
            ZoomLevel = 12
        };

        ViewBag.GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"] ?? "";

        return View(model);
    }
}
