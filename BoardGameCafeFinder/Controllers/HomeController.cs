using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BoardGameCafeFinder.Models;
using BoardGameCafeFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly BoardGameCafeFinder.Services.ICafeService _cafeService;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, BoardGameCafeFinder.Services.ICafeService cafeService)
    {
        _logger = logger;
        _context = context;
        _cafeService = cafeService;
    }

    public async Task<IActionResult> Index(
        string? country,
        string? city,
        int[]? gameIds,
        string? category,
        double? lat,
        double? lng,
        int radius = 50,
        int page = 1,
        int pageSize = 12)
    {
        // Default country to US if no filters are applied
        var isFirstVisit = string.IsNullOrEmpty(country) && string.IsNullOrEmpty(city)
                           && (gameIds == null || gameIds.Length == 0)
                           && !lat.HasValue && !lng.HasValue;

        if (isFirstVisit)
        {
            country = "United States";
        }

        // Set SEO metadata
        var pageTitle = "Board Game Cafes";
        var pageDescription = "Discover board game cafes near you. Find the best places to play modern board games with friends and family.";

        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(country))
        {
            pageTitle = $"Board Game Cafes in {city}, {country}";
            pageDescription = $"Find the best board game cafes in {city}, {country}. Browse games, read reviews, and plan your visit.";
        }
        else if (!string.IsNullOrEmpty(country))
        {
            pageTitle = $"Board Game Cafes in {country}";
            pageDescription = $"Explore board game cafes across {country}. Discover new games and connect with other board game enthusiasts.";
        }

        ViewData["Title"] = pageTitle;
        ViewData["MetaDescription"] = pageDescription;
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/";

        var cafesQuery = _context.Cafes
            .Where(c => c.IsActive)
            .Include(c => c.CafeGames)
                .ThenInclude(cg => cg.Game)
            .AsQueryable();

        // Filter by country
        if (!string.IsNullOrEmpty(country))
        {
            cafesQuery = cafesQuery.Where(c => c.Country == country);
        }

        // Filter by city
        if (!string.IsNullOrEmpty(city))
        {
            cafesQuery = cafesQuery.Where(c => c.City == city);
        }

        // Filter by board games
        if (gameIds != null && gameIds.Length > 0)
        {
            cafesQuery = cafesQuery.Where(c => c.CafeGames.Any(cg => gameIds.Contains(cg.GameId)));
        }

        // Filter by category
        if (!string.IsNullOrEmpty(category))
        {
            cafesQuery = cafesQuery.Where(c => c.CafeGames.Any(cg => cg.Game != null && cg.Game.Category == category));
        }

        // Get total count for pagination
        var totalItems = await cafesQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        // Get cafes with pagination
        List<BoardGameCafeFinder.Models.Domain.Cafe> cafes;

        if (lat.HasValue && lng.HasValue)
        {
            // Get all cafes first for distance calculation
            var allCafes = await cafesQuery.ToListAsync();

            foreach (var cafe in allCafes)
            {
                cafe.DistanceKm = CalculateDistance(lat.Value, lng.Value, cafe.Latitude, cafe.Longitude);
            }

            cafes = allCafes
                .Where(c => c.DistanceKm <= radius)
                .OrderBy(c => c.DistanceKm)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            totalItems = allCafes.Count(c => c.DistanceKm <= radius);
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        }
        else
        {
            cafes = await cafesQuery
                .OrderByDescending(c => c.AverageRating)
                .ThenByDescending(c => c.TotalReviews)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // Get filter options
        var countries = await _context.Cafes
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Country))
            .Select(c => c.Country)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var cities = await _context.Cafes
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City))
            .Where(c => c.Country == country)
            .Select(c => c.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // Get board games filtered by category if selected
        var boardGamesQuery = _context.BoardGames
            .Where(g => g.CafeGames.Any());

        if (!string.IsNullOrEmpty(category))
        {
            boardGamesQuery = boardGamesQuery.Where(g => g.Category == category);
        }

        var boardGames = await boardGamesQuery
            .OrderByDescending(g => g.CafeGames.Count)
            .ThenBy(g => g.Name)
            .Take(50)
            .Select(g => new { g.GameId, g.Name, g.Category, CafeCount = g.CafeGames.Count })
            .ToListAsync();

        // Get available categories
        var categories = await _context.BoardGames
            .Where(g => g.CafeGames.Any() && !string.IsNullOrEmpty(g.Category))
            .Select(g => g.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // ViewBag for filter values
        ViewBag.Countries = countries;
        ViewBag.Cities = cities;
        ViewBag.BoardGames = boardGames;
        ViewBag.Categories = categories;
        ViewBag.SelectedCountry = country;
        ViewBag.SelectedCity = city;
        ViewBag.SelectedCategory = category;
        ViewBag.SelectedGameIds = gameIds ?? Array.Empty<int>();
        ViewBag.UserLat = lat;
        ViewBag.UserLng = lng;
        ViewBag.Radius = radius;

        // Pagination info
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageSize = pageSize;

        return View(cafes);
    }

    // API endpoint to get cities by country (for dynamic dropdown)
    [HttpGet]
    public async Task<IActionResult> GetCitiesByCountry(string? country)
    {
        var citiesQuery = _context.Cafes
            .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City));

        if (!string.IsNullOrEmpty(country))
        {
            citiesQuery = citiesQuery.Where(c => c.Country == country);
        }

        var cities = await citiesQuery
            .Select(c => c.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Json(cities);
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in kilometers

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(R * c, 2);
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Privacy Policy";
        ViewData["MetaDescription"] = "Read our privacy policy to understand how we collect and protect your data.";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Privacy";
        return View();
    }

    public IActionResult Terms()
    {
        ViewData["Title"] = "Terms of Service";
        ViewData["MetaDescription"] = "Read the Terms of Service for Board Game Cafe Finder.";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/Home/Terms";
        return View();
    }

    public async Task<IActionResult> Details(int id)
    {
        var cafe = await _cafeService.GetByIdAsync(id);
        if (cafe == null)
        {
            return NotFound();
        }
        return View(cafe);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
