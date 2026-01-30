using EscapeRoomFinder.Models.DTOs;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoomFinder.Controllers
{
    public class MapController : Controller
    {
        private readonly IVenueService _venueService;
        private readonly ILogger<MapController> _logger;

        public MapController(IVenueService venueService, ILogger<MapController> logger)
        {
            _venueService = venueService;
            _logger = logger;
        }

        [Route("map")]
        public async Task<IActionResult> Index(
            string? city,
            string? theme,
            double? lat,
            double? lng)
        {
            var request = new VenueSearchRequest
            {
                City = city,
                Theme = theme,
                Latitude = lat,
                Longitude = lng,
                RadiusKm = 100,
                PageSize = 500 // Get all for map
            };

            var result = await _venueService.SearchVenuesPagedAsync(request);

            var model = new MapViewModel
            {
                Venues = result.Venues.Select(v => new MapVenueDto
                {
                    VenueId = v.VenueId,
                    Name = v.Name,
                    Slug = v.Slug,
                    Address = v.Address,
                    City = v.City,
                    Lat = v.Latitude,
                    Lng = v.Longitude,
                    Rating = v.AverageRating,
                    ReviewCount = v.TotalReviews,
                    RoomCount = v.TotalRooms,
                    ImageUrl = v.LocalImagePath,
                    IsPremium = v.IsPremium,
                    Themes = v.Rooms.Select(r => r.Theme).Distinct().ToList(),
                    DifficultyRange = v.LowestDifficulty.HasValue && v.HighestDifficulty.HasValue
                        ? $"{v.LowestDifficulty}-{v.HighestDifficulty}"
                        : null
                }).ToList(),
                CenterLat = lat ?? (result.Venues.Any() ? result.Venues.Average(v => v.Latitude) : 39.8283),
                CenterLng = lng ?? (result.Venues.Any() ? result.Venues.Average(v => v.Longitude) : -98.5795),
                ZoomLevel = city != null ? 11 : 4,
                City = city,
                Theme = theme
            };

            ViewBag.Cities = await _venueService.GetAllCitiesAsync();
            ViewBag.Themes = await _venueService.GetAllThemesAsync();

            return View(model);
        }

        [HttpGet]
        [Route("api/map/venues")]
        public async Task<IActionResult> GetVenuesForMap(
            double? lat,
            double? lng,
            double radius = 100,
            string? theme = null)
        {
            var request = new VenueSearchRequest
            {
                Latitude = lat,
                Longitude = lng,
                RadiusKm = radius,
                Theme = theme,
                PageSize = 500
            };

            var result = await _venueService.SearchVenuesPagedAsync(request);

            var venues = result.Venues.Select(v => new
            {
                v.VenueId,
                v.Name,
                v.Slug,
                v.Address,
                v.City,
                Lat = v.Latitude,
                Lng = v.Longitude,
                v.AverageRating,
                v.TotalReviews,
                v.TotalRooms,
                ImageUrl = v.LocalImagePath,
                v.IsPremium,
                Themes = v.Rooms.Select(r => r.Theme).Distinct()
            });

            return Json(venues);
        }
    }
}
