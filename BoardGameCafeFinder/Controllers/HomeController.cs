using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BoardGameCafeFinder.Models;
using BoardGameCafeFinder.Models.DTOs;
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
        string[]? categories,
        double? lat,
        double? lng,
        int radius = 50,
        int page = 1,
        int pageSize = 12)
    {
        // Check if this is a first visit with no filters
        var isFirstVisit = string.IsNullOrEmpty(country) && string.IsNullOrEmpty(city)
                           && (gameIds == null || gameIds.Length == 0)
                           && !lat.HasValue && !lng.HasValue;

        // SEO metadata will be set after we have cafe count
        var currentYear = DateTime.Now.Year;

        // SEO: Build canonical URL with filters (exclude pagination for canonical)
        var canonicalUrl = $"{Request.Scheme}://{Request.Host}/";
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(country)) queryParams.Add($"country={Uri.EscapeDataString(country)}");
        if (!string.IsNullOrEmpty(city)) queryParams.Add($"city={Uri.EscapeDataString(city)}");
        if (queryParams.Count > 0) canonicalUrl += "?" + string.Join("&", queryParams);
        ViewData["CanonicalUrl"] = canonicalUrl;

        // Build base query with filters (NO Include - use projection instead)
        var cafesQuery = _context.Cafes
            .Where(c => c.IsActive)
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

        // Filter by categories (multiselect)
        if (categories != null && categories.Length > 0)
        {
            cafesQuery = cafesQuery.Where(c => c.CafeGames.Any(cg => cg.Game != null && categories.Contains(cg.Game.Category)));
        }

        // Location-based filtering: use bounding box first for better SQL performance
        if (lat.HasValue && lng.HasValue)
        {
            // Calculate approximate bounding box (1 degree ≈ 111km)
            var latDelta = radius / 111.0;
            var lngDelta = radius / (111.0 * Math.Cos(lat.Value * Math.PI / 180.0));

            cafesQuery = cafesQuery.Where(c =>
                c.Latitude >= lat.Value - latDelta &&
                c.Latitude <= lat.Value + latDelta &&
                c.Longitude >= lng.Value - lngDelta &&
                c.Longitude <= lng.Value + lngDelta);
        }

        // Project to lightweight DTO - counts are calculated in SQL
        var projectedQuery = cafesQuery.Select(c => new CafeListItemDto
        {
            CafeId = c.CafeId,
            Name = c.Name,
            City = c.City,
            State = c.State,
            Country = c.Country,
            Slug = c.Slug,
            LocalImagePath = c.LocalImagePath,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            AverageRating = c.AverageRating,
            // Count reviews in SQL (UserId == null means Google review, skip IsApproved check)
            ReviewCount = c.Reviews.Count(r => r.UserId == null || r.IsApproved),
            GamesCount = c.CafeGames.Count,
            Website = c.Website,
            GoogleMapsUrl = c.GoogleMapsUrl
        });

        List<CafeListItemDto> cafes;
        int totalItems;
        int totalPages;

        if (lat.HasValue && lng.HasValue)
        {
            // Get filtered cafes and calculate precise distance in memory
            var boundedCafes = await projectedQuery.ToListAsync();

            foreach (var cafe in boundedCafes)
            {
                cafe.DistanceKm = CalculateDistance(lat.Value, lng.Value, cafe.Latitude, cafe.Longitude);
            }

            // Filter by exact radius and paginate
            var filteredCafes = boundedCafes
                .Where(c => c.DistanceKm <= radius)
                .OrderBy(c => c.DistanceKm)
                .ToList();

            totalItems = filteredCafes.Count;
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            cafes = filteredCafes
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            // Get total count for pagination
            totalItems = await cafesQuery.CountAsync();
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            cafes = await projectedQuery
                .OrderByDescending(c => c.AverageRating)
                .ThenByDescending(c => c.ReviewCount)
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

        // Get board games filtered by categories if selected
        var boardGamesQuery = _context.BoardGames
            .Where(g => g.CafeGames.Any());

        if (categories != null && categories.Length > 0)
        {
            boardGamesQuery = boardGamesQuery.Where(g => categories.Contains(g.Category));
        }

        var boardGames = await boardGamesQuery
            .OrderByDescending(g => g.CafeGames.Count)
            .ThenBy(g => g.Name)
            .Take(50)
            .Select(g => new { g.GameId, g.Name, g.Category, CafeCount = g.CafeGames.Count })
            .ToListAsync();

        // Get available categories
        var availableCategories = await _context.BoardGames
            .Where(g => g.CafeGames.Any() && !string.IsNullOrEmpty(g.Category))
            .Select(g => g.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        // ViewBag for filter values
        ViewBag.Countries = countries;
        ViewBag.Cities = cities;
        ViewBag.BoardGames = boardGames;
        ViewBag.Categories = availableCategories;
        ViewBag.SelectedCountry = country;
        ViewBag.SelectedCity = city;
        ViewBag.SelectedCategories = categories?.ToList() ?? new List<string>();
        ViewBag.SelectedGameIds = gameIds ?? Array.Empty<int>();
        ViewBag.UserLat = lat;
        ViewBag.UserLng = lng;
        ViewBag.Radius = radius;

        // Pagination info
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageSize = pageSize;
        ViewBag.IsFirstVisit = isFirstVisit;

        // Set SEO metadata with cafe count for better CTR
        string pageTitle;
        string pageDescription;

        if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(country))
        {
            pageTitle = $"{totalItems}+ Board Game Cafes in {city} ({currentYear}) | Reviews & Maps";
            pageDescription = $"Find {totalItems} board game cafes in {city}, {country}. Compare ratings, browse games, get directions. Updated {DateTime.Now:MMMM yyyy}.";
        }
        else if (!string.IsNullOrEmpty(country))
        {
            pageTitle = $"{totalItems}+ Board Game Cafes in {country} ({currentYear}) | Directory";
            pageDescription = $"Explore {totalItems} board game cafes across {country}. Discover games, read reviews and plan your visit.";
        }
        else
        {
            pageTitle = $"Find Board Game Cafes Near You | {totalItems}+ Locations";
            pageDescription = "Discover board game cafes near you. Browse games, read reviews, get directions. Find the perfect spot for your next gaming session!";
        }

        ViewData["Title"] = pageTitle;
        ViewData["MetaDescription"] = pageDescription;

        return View("Index", cafes);
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
