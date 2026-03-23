using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models;
using VRArcadeFinder.Models.DTOs;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IArcadeService _arcadeService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            IArcadeService arcadeService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _arcadeService = arcadeService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? country = null,
            string? city = null,
            int[]? gameIds = null,
            string[]? categories = null,
            string? vrPlatform = null,
            double? lat = null,
            double? lng = null,
            int? radius = null,
            int page = 1,
            int pageSize = 12)
        {
            try
            {
                // Get filter options
                ViewBag.Countries = await _context.Arcades
                    .Where(c => c.IsActive)
                    .Select(c => c.Country)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                ViewBag.Cities = await _context.Arcades
                    .Where(c => c.IsActive)
                    .Select(c => c.City)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                ViewBag.VRPlatforms = new List<string>
                {
                    "Meta Quest",
                    "HTC Vive",
                    "PlayStation VR",
                    "Valve Index",
                    "Windows Mixed Reality"
                };

                ViewBag.GameCategories = await _context.VRGames
                    .Where(g => !string.IsNullOrEmpty(g.Category))
                    .Select(g => g.Category!)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                // Set current filters
                ViewBag.SelectedCountry = country;
                ViewBag.SelectedCity = city;
                ViewBag.SelectedVRPlatform = vrPlatform;
                ViewBag.SelectedCategories = categories ?? Array.Empty<string>();
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;

                // Get arcades based on filters
                List<ArcadeListItemDto> arcades;

                if (lat.HasValue && lng.HasValue)
                {
                    // Location-based search
                    var searchRequest = new ArcadeSearchRequest
                    {
                        Latitude = lat.Value,
                        Longitude = lng.Value,
                        Radius = radius ?? 50000, // 50km default
                        Limit = 100
                    };

                    var results = await _arcadeService.SearchNearbyAsync(searchRequest);
                    arcades = results.Select(r => new ArcadeListItemDto
                    {
                        ArcadeId = r.Id,
                        Name = r.Name,
                        City = r.City,
                        State = r.State,
                        Country = country,
                        Slug = r.Slug,
                        Latitude = r.Latitude,
                        Longitude = r.Longitude,
                        AverageRating = r.AverageRating,
                        ReviewCount = r.TotalReviews,
                        GamesCount = r.TotalGames,
                        Website = r.Website,
                        VRPlatforms = r.VRPlatforms,
                        TotalVRStations = r.TotalVRStations,
                        DistanceKm = r.Distance / 1000.0
                    }).ToList();
                }
                else
                {
                    // Filter-based search
                    var query = _context.Arcades
                        .AsNoTracking()
                        .Where(c => c.IsActive);

                    if (!string.IsNullOrEmpty(country))
                    {
                        query = query.Where(c => c.Country == country);
                    }

                    if (!string.IsNullOrEmpty(city))
                    {
                        query = query.Where(c => c.City == city);
                    }

                    if (!string.IsNullOrEmpty(vrPlatform))
                    {
                        query = query.Where(c => c.VRPlatforms != null && c.VRPlatforms.Contains(vrPlatform));
                    }

                    if (categories != null && categories.Any())
                    {
                        query = query.Where(c => c.ArcadeGames.Any(ag => ag.Game != null && categories.Contains(ag.Game.Category)));
                    }

                    var totalCount = await query.CountAsync();
                    ViewBag.TotalCount = totalCount;
                    ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                    arcades = await query
                        .OrderByDescending(c => c.IsPremium)
                        .ThenByDescending(c => c.AverageRating)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new ArcadeListItemDto
                        {
                            ArcadeId = c.ArcadeId,
                            Name = c.Name,
                            City = c.City,
                            State = c.State,
                            Country = c.Country,
                            Slug = c.Slug,
                            LocalImagePath = c.LocalImagePath,
                            Latitude = c.Latitude,
                            Longitude = c.Longitude,
                            AverageRating = c.AverageRating,
                            ReviewCount = c.TotalReviews,
                            GamesCount = c.ArcadeGames.Count,
                            Website = c.Website,
                            GoogleMapsUrl = c.GoogleMapsUrl,
                            VRPlatforms = c.VRPlatforms,
                            TotalVRStations = c.TotalVRStations
                        })
                        .ToListAsync();
                }

                return View(arcades);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading home page");
                return View(new List<ArcadeListItemDto>());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
