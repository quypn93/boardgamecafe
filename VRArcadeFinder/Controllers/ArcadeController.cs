using Microsoft.AspNetCore.Mvc;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Controllers
{
    [Route("arcade")]
    public class ArcadeController : Controller
    {
        private readonly IArcadeService _arcadeService;
        private readonly ILogger<ArcadeController> _logger;

        public ArcadeController(
            IArcadeService arcadeService,
            ILogger<ArcadeController> logger)
        {
            _arcadeService = arcadeService;
            _logger = logger;
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrEmpty(slug))
            {
                return NotFound();
            }

            var arcade = await _arcadeService.GetBySlugAsync(slug);

            if (arcade == null)
            {
                return NotFound();
            }

            // Set SEO metadata
            var rating = arcade.AverageRating.HasValue ? $" - {arcade.AverageRating:F1}/5" : "";
            ViewBag.Title = $"{arcade.Name}{rating} | VR Arcade Finder";
            ViewBag.MetaDescription = arcade.MetaDescription ?? $"Visit {arcade.Name} in {arcade.City}. {arcade.Description?.Substring(0, Math.Min(150, arcade.Description?.Length ?? 0))}";

            return View(arcade);
        }
    }
}
