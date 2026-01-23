using BoardGameCafeFinder.Models.DTOs;
using BoardGameCafeFinder.Services;
using Microsoft.AspNetCore.Mvc;

namespace BoardGameCafeFinder.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ICafeService _cafeService;
        private readonly ILogger<ApiController> _logger;

        public ApiController(ICafeService cafeService, ILogger<ApiController> logger)
        {
            _cafeService = cafeService;
            _logger = logger;
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
    }
}
