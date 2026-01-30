using EscapeRoomFinder.Models;
using EscapeRoomFinder.Models.DTOs;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EscapeRoomFinder.Controllers
{
    public class HomeController : Controller
    {
        private readonly IVenueService _venueService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IVenueService venueService, ILogger<HomeController> logger)
        {
            _venueService = venueService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? query,
            string? city,
            string? theme,
            int? minDifficulty,
            int? maxDifficulty,
            int? players,
            double? lat,
            double? lng,
            double? radius,
            string? sort,
            int page = 1)
        {
            var request = new VenueSearchRequest
            {
                Query = query,
                City = city,
                Theme = theme,
                MinDifficulty = minDifficulty,
                MaxDifficulty = maxDifficulty,
                MinPlayers = players,
                MaxPlayers = players,
                Latitude = lat,
                Longitude = lng,
                RadiusKm = radius ?? 50,
                SortBy = sort,
                Page = page,
                PageSize = 20
            };

            var results = await _venueService.SearchVenuesPagedAsync(request);

            ViewBag.Cities = await _venueService.GetAllCitiesAsync();
            ViewBag.Themes = await _venueService.GetAllThemesAsync();
            ViewBag.TotalVenues = await _venueService.GetTotalVenueCountAsync();
            ViewBag.TotalRooms = await _venueService.GetTotalRoomCountAsync();

            ViewBag.Query = query;
            ViewBag.City = city;
            ViewBag.Theme = theme;
            ViewBag.MinDifficulty = minDifficulty;
            ViewBag.MaxDifficulty = maxDifficulty;
            ViewBag.Players = players;
            ViewBag.Sort = sort;
            ViewBag.CurrentPage = page;

            return View(results);
        }

        // Details action moved to VenueController to avoid route conflict

        [Route("privacy")]
        public IActionResult Privacy()
        {
            return View();
        }

        [Route("terms")]
        public IActionResult Terms()
        {
            return View();
        }

        [Route("about")]
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
