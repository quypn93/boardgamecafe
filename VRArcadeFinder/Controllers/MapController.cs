using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.DTOs;

namespace VRArcadeFinder.Controllers
{
    [Route("map")]
    public class MapController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MapController> _logger;

        // City coordinates for initial map positioning
        private static readonly Dictionary<string, (double Lat, double Lng)> CityCoordinates = new()
        {
            { "New York", (40.7128, -74.0060) },
            { "Los Angeles", (34.0522, -118.2437) },
            { "Chicago", (41.8781, -87.6298) },
            { "Houston", (29.7604, -95.3698) },
            { "Phoenix", (33.4484, -112.0740) },
            { "San Francisco", (37.7749, -122.4194) },
            { "Seattle", (47.6062, -122.3321) },
            { "Denver", (39.7392, -104.9903) },
            { "Austin", (30.2672, -97.7431) },
            { "Las Vegas", (36.1699, -115.1398) },
            { "Tokyo", (35.6762, 139.6503) },
            { "Seoul", (37.5665, 126.9780) },
            { "London", (51.5074, -0.1278) },
            { "Berlin", (52.5200, 13.4050) },
            { "Paris", (48.8566, 2.3522) },
            { "Sydney", (-33.8688, 151.2093) },
            { "Toronto", (43.6532, -79.3832) },
            { "Singapore", (1.3521, 103.8198) },
            { "Hong Kong", (22.3193, 114.1694) },
            { "Dubai", (25.2048, 55.2708) }
        };

        public MapController(
            ApplicationDbContext context,
            ILogger<MapController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? city = null, string? country = null, double? lat = null, double? lon = null)
        {
            var viewModel = new MapViewModel
            {
                SelectedCity = city,
                SelectedCountry = country
            };

            // Get filter options
            viewModel.Countries = await _context.Arcades
                .Where(a => a.IsActive)
                .Select(a => a.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            viewModel.Cities = await _context.Arcades
                .Where(a => a.IsActive)
                .Select(a => a.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            viewModel.VRPlatforms = new List<string>
            {
                "Meta Quest",
                "HTC Vive",
                "PlayStation VR",
                "Valve Index",
                "Windows Mixed Reality"
            };

            viewModel.GameCategories = await _context.VRGames
                .Where(g => !string.IsNullOrEmpty(g.Category))
                .Select(g => g.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Set initial map position
            if (lat.HasValue && lon.HasValue)
            {
                viewModel.InitialLat = lat.Value;
                viewModel.InitialLng = lon.Value;
                viewModel.InitialZoom = 13;
            }
            else if (!string.IsNullOrEmpty(city) && CityCoordinates.TryGetValue(city, out var coords))
            {
                viewModel.InitialLat = coords.Lat;
                viewModel.InitialLng = coords.Lng;
                viewModel.InitialZoom = 12;
            }
            else
            {
                // Default to US center
                viewModel.InitialLat = 39.8283;
                viewModel.InitialLng = -98.5795;
                viewModel.InitialZoom = 4;
            }

            return View(viewModel);
        }
    }
}
