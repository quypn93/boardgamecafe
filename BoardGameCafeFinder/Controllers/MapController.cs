using BoardGameCafeFinder.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameCafeFinder.Controllers
{
    public class MapController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MapController> _logger;

        public MapController(IConfiguration configuration, ILogger<MapController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index(string? city, double? lat, double? lon)
        {
            var model = new MapViewModel
            {
                GoogleMapsApiKey = _configuration["GooglePlaces:ApiKey"] ?? "",
                InitialCity = city,
                InitialLatitude = lat,
                InitialLongitude = lon
            };

            // If city is provided, set initial coordinates (hardcoded for now)
            if (!string.IsNullOrEmpty(city))
            {
                var cityCoordinates = GetCityCoordinates(city);
                model.InitialLatitude = cityCoordinates.lat;
                model.InitialLongitude = cityCoordinates.lon;
                model.DefaultZoom = 12;
            }

            _logger.LogInformation("Map page accessed with city: {City}, lat: {Lat}, lon: {Lon}", city, lat, lon);

            return View(model);
        }

        private (double lat, double lon) GetCityCoordinates(string city)
        {
            // Hardcoded coordinates for major cities (replace with geocoding API later)
            return city.ToLower() switch
            {
                "seattle" => (47.6062, -122.3321),
                "portland" => (45.5152, -122.6784),
                "chicago" => (41.8781, -87.6298),
                "new york" => (40.7128, -74.0060),
                "los angeles" => (34.0522, -118.2437),
                "san francisco" => (37.7749, -122.4194),
                "austin" => (30.2672, -97.7431),
                "denver" => (39.7392, -104.9903),
                _ => (39.8283, -98.5795) // Default to USA center
            };
        }
    }
}
