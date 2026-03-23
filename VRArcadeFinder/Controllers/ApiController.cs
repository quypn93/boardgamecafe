using Microsoft.AspNetCore.Mvc;
using VRArcadeFinder.Models.DTOs;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly IArcadeService _arcadeService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(
            IArcadeService arcadeService,
            ILogger<ApiController> logger)
        {
            _arcadeService = arcadeService;
            _logger = logger;
        }

        /// <summary>
        /// Search for arcades near a location
        /// </summary>
        [HttpPost("arcades/search")]
        public async Task<IActionResult> SearchArcades([FromBody] ArcadeSearchRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var results = await _arcadeService.SearchNearbyAsync(request);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching arcades");
                return StatusCode(500, new { error = "An error occurred while searching" });
            }
        }

        /// <summary>
        /// Search arcades by text query
        /// </summary>
        [HttpGet("arcades/search")]
        public async Task<IActionResult> SearchByText([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { error = "Query parameter 'q' is required" });
            }

            try
            {
                var results = await _arcadeService.SearchByTextAsync(q, limit);
                return Ok(results.Select(a => new
                {
                    id = a.ArcadeId,
                    name = a.Name,
                    city = a.City,
                    country = a.Country,
                    slug = a.Slug
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching arcades by text");
                return StatusCode(500, new { error = "An error occurred while searching" });
            }
        }
    }
}
