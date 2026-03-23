using CookingClassFinder.Models;
using CookingClassFinder.Models.DTOs;
using CookingClassFinder.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CookingClassFinder.Controllers
{
    public class HomeController : Controller
    {
        private readonly ISchoolService _schoolService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ISchoolService schoolService, ILogger<HomeController> logger)
        {
            _schoolService = schoolService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? query, string? city, string? cuisine)
        {
            var request = new SchoolSearchRequest
            {
                Query = query,
                City = city,
                CuisineType = cuisine,
                PageSize = 20
            };

            var schools = await _schoolService.SearchSchoolsPagedAsync(request);
            var cities = await _schoolService.GetAllCitiesAsync();
            var cuisines = await _schoolService.GetAllCuisineTypesAsync();

            ViewBag.Query = query;
            ViewBag.City = city;
            ViewBag.Cuisine = cuisine;
            ViewBag.Cities = cities;
            ViewBag.Cuisines = cuisines;
            ViewBag.TotalSchools = await _schoolService.GetTotalSchoolCountAsync();
            ViewBag.TotalClasses = await _schoolService.GetTotalClassCountAsync();

            return View(schools);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult ForBusiness()
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
