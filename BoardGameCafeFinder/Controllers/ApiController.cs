using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using BoardGameCafeFinder.Models.DTOs;
using BoardGameCafeFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ICafeService _cafeService;
        private readonly ILogger<ApiController> _logger;
        private readonly ApplicationDbContext _context;

        public ApiController(ICafeService cafeService, ILogger<ApiController> logger, ApplicationDbContext context)
        {
            _cafeService = cafeService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Search for cafés near a location
        /// </summary>
        /// <param name="request">Search parameters</param>
        /// <returns>List of cafés with distances</returns>
        [HttpPost("cafes/search")]
        [ProducesResponseType(typeof(List<CafeSearchResultDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SearchCafes([FromBody] CafeSearchRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation(
                    "Café search request: Lat={Latitude}, Lon={Longitude}, Radius={Radius}m, OpenNow={OpenNow}, HasGames={HasGames}",
                    request.Latitude, request.Longitude, request.Radius, request.OpenNow, request.HasGames);

                // Search cafés
                var results = await _cafeService.SearchNearbyAsync(request);

                return Ok(new
                {
                    success = true,
                    count = results.Count,
                    data = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching cafés");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while searching for cafés. Please try again later."
                });
            }
        }

        /// <summary>
        /// Simple text search for cafés by name or city
        /// </summary>
        [HttpGet("cafes/search")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SearchCafesByText([FromQuery] string q, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                {
                    return Ok(new List<object>());
                }

                var results = await _cafeService.SearchByTextAsync(q, limit);

                return Ok(results.Select(cafe => new
                {
                    cafeId = cafe.CafeId,
                    name = cafe.Name,
                    city = cafe.City,
                    country = cafe.Country,
                    localImagePath = cafe.LocalImagePath,
                    slug = cafe.Slug
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching cafés by text: {Query}", q);
                return StatusCode(500, new List<object>());
            }
        }

        /// <summary>
        /// Get café details by ID
        /// </summary>
        [HttpGet("cafes/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCafe(int id)
        {
            try
            {
                var cafe = await _cafeService.GetByIdAsync(id);

                if (cafe == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Café not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = cafe.CafeId,
                        name = cafe.Name,
                        description = cafe.Description,
                        address = cafe.Address,
                        city = cafe.City,
                        state = cafe.State,
                        phone = cafe.Phone,
                        website = cafe.Website,
                        latitude = cafe.Latitude,
                        longitude = cafe.Longitude,
                        averageRating = cafe.AverageRating,
                        totalReviews = cafe.TotalReviews,
                        isOpenNow = cafe.IsOpenNow(),
                        openingHours = cafe.GetOpeningHours(),
                        priceRange = cafe.PriceRange,
                        photos = cafe.Photos.Select(p => new { url = p.Url, caption = p.Caption }),
                        totalGames = cafe.CafeGames.Count,
                        slug = cafe.Slug
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting café with ID {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving café details"
                });
            }
        }

        /// <summary>
        /// Get café details by slug (for debugging coordinates)
        /// </summary>
        [HttpGet("cafes/slug/{slug}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCafeBySlug(string slug)
        {
            try
            {
                var cafe = await _cafeService.GetBySlugAsync(slug);

                if (cafe == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Café not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = cafe.CafeId,
                        name = cafe.Name,
                        slug = cafe.Slug,
                        address = cafe.Address,
                        city = cafe.City,
                        country = cafe.Country,
                        latitude = cafe.Latitude,
                        longitude = cafe.Longitude,
                        googleMapsUrl = cafe.GoogleMapsUrl,
                        // Debug info
                        coordinateInfo = new
                        {
                            storedLatitude = cafe.Latitude,
                            storedLongitude = cafe.Longitude,
                            isLatitudeValid = cafe.Latitude >= -90 && cafe.Latitude <= 90,
                            isLongitudeValid = cafe.Longitude >= -180 && cafe.Longitude <= 180,
                            googleMapsLink = $"https://www.google.com/maps?q={cafe.Latitude},{cafe.Longitude}"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting café with slug {Slug}", slug);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving café details"
                });
            }
        }

        /// <summary>
        /// Filter cafés by country/city/categories
        /// </summary>
        [HttpGet("cafes/filter")]
        [ProducesResponseType(typeof(List<CafeSearchResultDto>), 200)]
        public async Task<IActionResult> FilterCafes(
            [FromQuery] string? country = null,
            [FromQuery] string? city = null,
            [FromQuery] bool openNow = false,
            [FromQuery] bool hasGames = false,
            [FromQuery] double? minRating = null,
            [FromQuery] string? categories = null)
        {
            try
            {
                // Parse categories from comma-separated string
                var categoryList = string.IsNullOrEmpty(categories)
                    ? null
                    : categories.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();

                _logger.LogInformation(
                    "Café filter request: Country={Country}, City={City}, OpenNow={OpenNow}, HasGames={HasGames}, Categories={Categories}",
                    country, city, openNow, hasGames, categories);

                var results = await _cafeService.FilterCafesAsync(country, city, openNow, hasGames, minRating, categoryList);

                return Ok(new
                {
                    success = true,
                    count = results.Count,
                    data = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while filtering cafés");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while filtering cafés. Please try again later."
                });
            }
        }

        /// <summary>
        /// Get popular cities with café counts
        /// </summary>
        [HttpGet("cities")]
        [ProducesResponseType(200)]
        public Task<IActionResult> GetCities()
        {
            try
            {
                // This is a placeholder - implement actual city aggregation later
                var cities = new[]
                {
                    new { city = "Seattle", state = "WA", count = 15 },
                    new { city = "Portland", state = "OR", count = 12 },
                    new { city = "Chicago", state = "IL", count = 18 },
                    new { city = "New York", state = "NY", count = 25 },
                    new { city = "Los Angeles", state = "CA", count = 20 }
                };

                return Task.FromResult<IActionResult>(Ok(new
                {
                    success = true,
                    data = cities
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cities");
                return Task.FromResult<IActionResult>(StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving cities"
                }));
            }
        }

        /// <summary>
        /// Track affiliate link click and redirect
        /// </summary>
        [HttpGet("affiliate/click/{gameId}")]
        public async Task<IActionResult> TrackAffiliateClick(int gameId, [FromQuery] int? cafeId = null)
        {
            try
            {
                var game = await _context.BoardGames.FindAsync(gameId);
                if (game == null || string.IsNullOrEmpty(game.AmazonAffiliateUrl))
                {
                    return NotFound(new { success = false, message = "Game or affiliate URL not found" });
                }

                // Record the click
                var click = new AffiliateClick
                {
                    GameId = gameId,
                    CafeId = cafeId,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers.UserAgent.ToString().Length > 500
                        ? Request.Headers.UserAgent.ToString()[..500]
                        : Request.Headers.UserAgent.ToString(),
                    Referrer = Request.Headers.Referer.ToString().Length > 500
                        ? Request.Headers.Referer.ToString()[..500]
                        : Request.Headers.Referer.ToString(),
                    ClickedAt = DateTime.UtcNow
                };

                // Get user ID if logged in
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    click.UserId = userId;
                }

                _context.AffiliateClicks.Add(click);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Affiliate click tracked: GameId={GameId}, CafeId={CafeId}", gameId, cafeId);

                // Redirect to affiliate URL
                return Redirect(game.AmazonAffiliateUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking affiliate click for game {GameId}", gameId);

                // Still try to redirect even if tracking fails
                var game = await _context.BoardGames.FindAsync(gameId);
                if (game != null && !string.IsNullOrEmpty(game.AmazonAffiliateUrl))
                {
                    return Redirect(game.AmazonAffiliateUrl);
                }

                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        private string? GetClientIpAddress()
        {
            // Check for forwarded IP (behind proxy/load balancer)
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',').FirstOrDefault()?.Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
