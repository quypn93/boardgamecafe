using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.ViewModels;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogService _blogService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BlogController> _logger;

        public BlogController(IBlogService blogService, ApplicationDbContext context, ILogger<BlogController> logger)
        {
            _blogService = blogService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Blog index - shows cities with escape rooms as dynamic content
        /// </summary>
        [Route("blog")]
        public async Task<IActionResult> Index(string? country = null, int page = 1)
        {
            const int pageSize = 24;

            var venuesQuery = _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.City));

            if (!string.IsNullOrEmpty(country))
            {
                venuesQuery = venuesQuery.Where(v => v.Country == country);
            }

            var venuesData = await venuesQuery
                .Select(v => new
                {
                    v.City,
                    v.Country,
                    v.AverageRating,
                    v.LocalImagePath,
                    RoomCount = v.Rooms.Count(r => r.IsActive)
                })
                .ToListAsync();

            var allCities = venuesData
                .GroupBy(v => new { v.City, v.Country })
                .Select(g => new CityBlogItem
                {
                    City = g.Key.City,
                    Country = g.Key.Country,
                    VenueCount = g.Count(),
                    TotalRooms = g.Sum(v => v.RoomCount),
                    AverageRating = g.Where(v => v.AverageRating.HasValue).Any()
                        ? g.Where(v => v.AverageRating.HasValue).Average(v => v.AverageRating!.Value)
                        : null,
                    SampleImageUrl = g.FirstOrDefault(v => !string.IsNullOrEmpty(v.LocalImagePath))?.LocalImagePath
                })
                .OrderByDescending(c => c.VenueCount)
                .ThenBy(c => c.City)
                .ToList();

            // Get countries for filter
            ViewBag.Countries = await _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Country))
                .Select(v => v.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.SelectedCountry = country;
            ViewBag.TotalCities = allCities.Count;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(allCities.Count / (double)pageSize);

            var pagedCities = allCities
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(pagedCities);
        }

        [Route("blog/{slug}")]
        public async Task<IActionResult> Post(string slug)
        {
            // First check if it's a city guide (not a blog post)
            var cityGuide = await TryGetCityGuide(slug);
            if (cityGuide != null)
            {
                return View("CityGuide", cityGuide);
            }

            // Otherwise, try to get blog post
            var post = await _blogService.GetPostBySlugAsync(slug);

            if (post == null)
            {
                return NotFound();
            }

            // Increment view count
            await _blogService.IncrementViewCountAsync(post.Id);

            // Get related posts
            if (!string.IsNullOrEmpty(post.RelatedCity))
            {
                ViewBag.RelatedPosts = await _blogService.GetPostsByCityAsync(post.RelatedCity, 3);
            }
            else if (!string.IsNullOrEmpty(post.Category))
            {
                ViewBag.RelatedPosts = await _blogService.GetPostsByCategoryAsync(post.Category, 3);
            }

            return View(post);
        }

        [Route("blog/category/{category}")]
        public async Task<IActionResult> Category(string category, int page = 1)
        {
            var posts = await _blogService.GetPostsByCategoryAsync(category, (page - 1) * 10 + 10);
            ViewBag.Category = category;
            ViewBag.Page = page;

            return View("Index", posts.Skip((page - 1) * 10).Take(10).ToList());
        }

        [Route("blog/city/{city}")]
        public async Task<IActionResult> City(string city, int page = 1)
        {
            var posts = await _blogService.GetPostsByCityAsync(city, (page - 1) * 10 + 10);
            ViewBag.City = city;
            ViewBag.Page = page;

            return View("Index", posts.Skip((page - 1) * 10).Take(10).ToList());
        }

        /// <summary>
        /// Dynamic city listings page - shows all cities with escape rooms
        /// </summary>
        [Route("cities")]
        public async Task<IActionResult> Cities(string? country = null, int page = 1)
        {
            const int pageSize = 24;

            var venuesQuery = _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.City));

            if (!string.IsNullOrEmpty(country))
            {
                venuesQuery = venuesQuery.Where(v => v.Country == country);
            }

            var venuesData = await venuesQuery
                .Select(v => new
                {
                    v.City,
                    v.Country,
                    v.AverageRating,
                    v.LocalImagePath,
                    RoomCount = v.Rooms.Count(r => r.IsActive)
                })
                .ToListAsync();

            var allCities = venuesData
                .GroupBy(v => new { v.City, v.Country })
                .Select(g => new CityBlogItem
                {
                    City = g.Key.City,
                    Country = g.Key.Country,
                    VenueCount = g.Count(),
                    TotalRooms = g.Sum(v => v.RoomCount),
                    AverageRating = g.Where(v => v.AverageRating.HasValue).Any()
                        ? g.Where(v => v.AverageRating.HasValue).Average(v => v.AverageRating!.Value)
                        : null,
                    SampleImageUrl = g.FirstOrDefault(v => !string.IsNullOrEmpty(v.LocalImagePath))?.LocalImagePath
                })
                .OrderByDescending(c => c.VenueCount)
                .ThenBy(c => c.City)
                .ToList();

            // Get countries for filter
            ViewBag.Countries = await _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Country))
                .Select(v => v.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.SelectedCountry = country;
            ViewBag.TotalCities = allCities.Count;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(allCities.Count / (double)pageSize);

            var pagedCities = allCities
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(pagedCities);
        }

        /// <summary>
        /// Individual city guide page - shows all escape rooms in a city
        /// </summary>
        [Route("escape-rooms-in/{city}")]
        public async Task<IActionResult> CityGuide(string city)
        {
            var viewModel = await TryGetCityGuide(city);

            if (viewModel == null)
            {
                return NotFound();
            }

            return View(viewModel);
        }

        private async Task<CityGuideViewModel?> TryGetCityGuide(string citySlug)
        {
            // Convert slug to city name pattern: "new-york" -> "New York"
            var cityPattern = citySlug.Replace("-", " ");

            // Find venues in this city
            var venues = await _context.Venues
                .Include(v => v.Rooms.Where(r => r.IsActive))
                .Where(v => v.IsActive && EF.Functions.Like(v.City.ToLower(), cityPattern.ToLower()))
                .OrderByDescending(v => v.AverageRating)
                .ThenByDescending(v => v.TotalReviews)
                .ToListAsync();

            if (!venues.Any())
            {
                return null;
            }

            var firstVenue = venues.First();
            var country = firstVenue.Country;
            var actualCityName = firstVenue.City;

            // Get related cities in same country
            var relatedCities = await _context.Venues
                .Where(v => v.IsActive && v.Country == country && v.City != actualCityName)
                .GroupBy(v => v.City)
                .Select(g => new CityBlogItem
                {
                    City = g.Key,
                    Country = country,
                    VenueCount = g.Count()
                })
                .OrderByDescending(c => c.VenueCount)
                .Take(6)
                .ToListAsync();

            return new CityGuideViewModel
            {
                City = actualCityName,
                Country = country,
                Venues = venues,
                TotalRooms = venues.Sum(v => v.Rooms.Count),
                AverageRating = venues.Where(v => v.AverageRating.HasValue).Any()
                    ? venues.Where(v => v.AverageRating.HasValue).Average(v => v.AverageRating!.Value)
                    : null,
                RelatedCities = relatedCities
            };
        }
    }
}
