using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using System.Globalization;

namespace VRArcadeFinder.Controllers
{
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BlogController> _logger;

        // Map culture codes to default countries
        private static readonly Dictionary<string, string> CultureToCountry = new(StringComparer.OrdinalIgnoreCase)
        {
            { "vi", "Vietnam" },
            { "ja", "Japan" },
            { "ko", "South Korea" },
            { "zh", "China" },
            { "th", "Thailand" },
            { "es", "Spain" },
            { "de", "Germany" }
            // "en" - no default country, show all
        };

        public BlogController(ApplicationDbContext context, ILogger<BlogController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Blog listing page - shows cities as dynamic blog posts
        /// Automatically filters by country based on user's language when no country specified
        /// </summary>
        [Route("blog")]
        public async Task<IActionResult> Index(string? country = null, int page = 1)
        {
            const int pageSize = 12;

            // If no country specified, try to get default country from user's language
            var currentCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string? defaultCountry = null;

            if (string.IsNullOrEmpty(country) && CultureToCountry.TryGetValue(currentCulture, out var mappedCountry))
            {
                defaultCountry = mappedCountry;
            }

            // Load arcades with game counts first
            var arcadesQuery = _context.Arcades
                .Where(a => a.IsActive && !string.IsNullOrEmpty(a.City));

            // Filter by country if specified (explicit filter takes priority)
            if (!string.IsNullOrEmpty(country))
            {
                arcadesQuery = arcadesQuery.Where(a => a.Country == country);
                ViewBag.SelectedCountry = country;
            }
            else if (!string.IsNullOrEmpty(defaultCountry))
            {
                // Check if the default country has any arcades
                var hasDefaultCountryArcades = await _context.Arcades
                    .AnyAsync(a => a.IsActive && a.Country == defaultCountry);

                if (hasDefaultCountryArcades)
                {
                    arcadesQuery = arcadesQuery.Where(a => a.Country == defaultCountry);
                    ViewBag.SelectedCountry = defaultCountry;
                    ViewBag.AutoFiltered = true; // Flag to indicate auto-filtering
                }
            }

            // Load data into memory, then group
            var arcadesData = await arcadesQuery
                .Select(a => new
                {
                    a.City,
                    a.Country,
                    a.AverageRating,
                    a.LocalImagePath,
                    GameCount = a.ArcadeGames != null ? a.ArcadeGames.Count() : 0
                })
                .ToListAsync();

            // Group in memory
            var allCities = arcadesData
                .GroupBy(a => new { a.City, a.Country })
                .Select(g => new CityBlogItem
                {
                    City = g.Key.City,
                    Country = g.Key.Country,
                    ArcadeCount = g.Count(),
                    TotalGames = g.Sum(a => a.GameCount),
                    AverageRating = g.Where(a => a.AverageRating.HasValue).Average(a => (double?)a.AverageRating) ?? 0,
                    SampleImage = g.OrderByDescending(a => a.AverageRating).Select(a => a.LocalImagePath).FirstOrDefault()
                })
                .OrderByDescending(c => c.ArcadeCount)
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
            var countries = await _context.Arcades
                .Where(a => a.IsActive && !string.IsNullOrEmpty(a.Country))
                .Select(a => a.Country)
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
        /// Supports both slug format (ho-chi-minh) and URL-encoded format (Ho%20Chi%20Minh)
        /// </summary>
        [Route("blog/{city}")]
        public async Task<IActionResult> Post(string city)
        {
            // First, decode URL encoding for backward compatibility
            city = Uri.UnescapeDataString(city);

            // Try to find city by exact match first
            var arcades = await _context.Arcades
                .Include(a => a.ArcadeGames!)
                    .ThenInclude(ag => ag.Game)
                .Include(a => a.Photos)
                .Where(a => a.IsActive && EF.Functions.Like(a.City, city))
                .OrderByDescending(a => a.AverageRating)
                .ThenByDescending(a => a.TotalReviews)
                .ToListAsync();

            // If no match, try matching by slug (convert slug to pattern: "ho-chi-minh" matches "Ho Chi Minh")
            if (!arcades.Any())
            {
                // Convert slug to match pattern: replace hyphens with spaces for LIKE comparison
                var slugPattern = city.Replace("-", " ");
                arcades = await _context.Arcades
                    .Include(a => a.ArcadeGames!)
                        .ThenInclude(ag => ag.Game)
                    .Include(a => a.Photos)
                    .Where(a => a.IsActive && EF.Functions.Like(a.City.ToLower(), slugPattern.ToLower()))
                    .OrderByDescending(a => a.AverageRating)
                    .ThenByDescending(a => a.TotalReviews)
                    .ToListAsync();

                // Update city name to the actual value from database
                if (arcades.Any())
                {
                    city = arcades.First().City;
                }
            }

            if (!arcades.Any())
            {
                return NotFound();
            }

            var firstArcade = arcades.First();
            var country = firstArcade.Country;

            // Get top games in this city
            var topGames = await _context.ArcadeGames
                .Where(ag => arcades.Select(a => a.ArcadeId).Contains(ag.ArcadeId))
                .GroupBy(ag => ag.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    ArcadeCount = g.Count()
                })
                .OrderByDescending(g => g.ArcadeCount)
                .Take(10)
                .ToListAsync();

            var topGameIds = topGames.Select(g => g.GameId).ToList();
            var games = await _context.VRGames
                .Where(g => topGameIds.Contains(g.GameId))
                .ToListAsync();

            // Create dynamic post data
            var postData = new DynamicBlogPost
            {
                City = city,
                Country = country,
                Arcades = arcades,
                TopGames = games.Select(g => new GameWithCount
                {
                    Game = g,
                    ArcadeCount = topGames.FirstOrDefault(tg => tg.GameId == g.GameId)?.ArcadeCount ?? 0
                }).OrderByDescending(g => g.ArcadeCount).ToList(),
                TotalArcades = arcades.Count,
                TotalGames = arcades.Sum(a => a.ArcadeGames?.Count ?? 0),
                AverageRating = arcades.Where(a => a.AverageRating.HasValue).Average(a => (double?)a.AverageRating) ?? 0
            };

            // Get related cities (same country)
            var relatedCities = await _context.Arcades
                .Where(a => a.IsActive && a.Country == country && !EF.Functions.Like(a.City, city))
                .GroupBy(a => a.City)
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
        public int ArcadeCount { get; set; }
        public int TotalGames { get; set; }
        public double AverageRating { get; set; }
        public string? SampleImage { get; set; }

        public string Slug => City.ToLower().Replace(" ", "-");
    }

    public class DynamicBlogPost
    {
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public List<Arcade> Arcades { get; set; } = new();
        public List<GameWithCount> TopGames { get; set; } = new();
        public int TotalArcades { get; set; }
        public int TotalGames { get; set; }
        public double AverageRating { get; set; }
    }

    public class GameWithCount
    {
        public VRGame Game { get; set; } = null!;
        public int ArcadeCount { get; set; }
    }
}
