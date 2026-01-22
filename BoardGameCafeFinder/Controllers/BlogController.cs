using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Controllers
{
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BlogController> _logger;

        public BlogController(ApplicationDbContext context, ILogger<BlogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Blog listing page - shows cities as dynamic blog posts
        /// </summary>
        public async Task<IActionResult> Index(string? country = null, int page = 1)
        {
            const int pageSize = 12;

            // Load cafes with game counts first (EF Core can't translate complex GroupBy with navigation)
            var cafesQuery = _context.Cafes
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City));

            // Filter by country if specified
            if (!string.IsNullOrEmpty(country))
            {
                cafesQuery = cafesQuery.Where(c => c.Country == country);
                ViewBag.SelectedCountry = country;
            }

            // Load data into memory, then group
            var cafesData = await cafesQuery
                .Select(c => new
                {
                    c.City,
                    c.Country,
                    c.AverageRating,
                    c.LocalImagePath,
                    GameCount = c.CafeGames != null ? c.CafeGames.Count() : 0
                })
                .ToListAsync();

            // Group in memory
            var allCities = cafesData
                .GroupBy(c => new { c.City, c.Country })
                .Select(g => new CityBlogItem
                {
                    City = g.Key.City,
                    Country = g.Key.Country,
                    CafeCount = g.Count(),
                    TotalGames = g.Sum(c => c.GameCount),
                    AverageRating = g.Where(c => c.AverageRating.HasValue).Average(c => (double?)c.AverageRating) ?? 0,
                    SampleImage = g.OrderByDescending(c => c.AverageRating).Select(c => c.LocalImagePath).FirstOrDefault()
                })
                .OrderByDescending(c => c.CafeCount)
                .ThenBy(c => c.City)
                .ToList();

            // Get total count for pagination
            var totalItems = allCities.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Get paginated results
            var cities = allCities
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Get distinct countries for filter
            var countries = await _context.Cafes
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.Country))
                .Select(c => c.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Countries = countries;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(cities);
        }

        /// <summary>
        /// Dynamic blog post page - generates content from database
        /// </summary>
        [Route("blog/{city}")]
        public async Task<IActionResult> Post(string city)
        {
            // Decode the city name
            city = Uri.UnescapeDataString(city);

            // Get cafes in this city
            var cafes = await _context.Cafes
                .Include(c => c.CafeGames!)
                    .ThenInclude(cg => cg.Game)
                .Include(c => c.Photos)
                .Where(c => c.IsActive && EF.Functions.Like(c.City, city))
                .OrderByDescending(c => c.AverageRating)
                .ThenByDescending(c => c.TotalReviews)
                .ToListAsync();

            if (!cafes.Any())
            {
                return NotFound();
            }

            var firstCafe = cafes.First();
            var country = firstCafe.Country;

            // Get top games in this city
            var topGames = await _context.CafeGames
                .Where(cg => cafes.Select(c => c.CafeId).Contains(cg.CafeId))
                .GroupBy(cg => cg.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    CafeCount = g.Count()
                })
                .OrderByDescending(g => g.CafeCount)
                .Take(10)
                .ToListAsync();

            var topGameIds = topGames.Select(g => g.GameId).ToList();
            var games = await _context.BoardGames
                .Where(g => topGameIds.Contains(g.GameId))
                .ToListAsync();

            // Create dynamic post data
            var postData = new DynamicBlogPost
            {
                City = city,
                Country = country,
                Cafes = cafes,
                TopGames = games.Select(g => new GameWithCount
                {
                    Game = g,
                    CafeCount = topGames.FirstOrDefault(tg => tg.GameId == g.GameId)?.CafeCount ?? 0
                }).OrderByDescending(g => g.CafeCount).ToList(),
                TotalCafes = cafes.Count,
                TotalGames = cafes.Sum(c => c.CafeGames?.Count ?? 0),
                AverageRating = cafes.Where(c => c.AverageRating.HasValue).Average(c => (double?)c.AverageRating) ?? 0
            };

            // Get related cities (same country)
            var relatedCities = await _context.Cafes
                .Where(c => c.IsActive && c.Country == country && !EF.Functions.Like(c.City, city))
                .GroupBy(c => c.City)
                .Select(g => new { City = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            ViewBag.RelatedCities = relatedCities.Select(r => r.City).ToList();

            return View(postData);
        }
    }

    // DTO classes for dynamic blog
    public class CityBlogItem
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public int CafeCount { get; set; }
        public int TotalGames { get; set; }
        public double AverageRating { get; set; }
        public string? SampleImage { get; set; }

        public string Slug => Uri.EscapeDataString(City.ToLower().Replace(" ", "-"));
    }

    public class DynamicBlogPost
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public List<Cafe> Cafes { get; set; } = new();
        public List<GameWithCount> TopGames { get; set; } = new();
        public int TotalCafes { get; set; }
        public int TotalGames { get; set; }
        public double AverageRating { get; set; }
    }

    public class GameWithCount
    {
        public BoardGame Game { get; set; } = null!;
        public int CafeCount { get; set; }
    }
}
