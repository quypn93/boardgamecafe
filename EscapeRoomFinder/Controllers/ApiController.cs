using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Models.DTOs;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly IVenueService _venueService;
        private readonly ILogger<ApiController> _logger;
        private readonly ApplicationDbContext _context;

        public ApiController(IVenueService venueService, ILogger<ApiController> logger, ApplicationDbContext context)
        {
            _venueService = venueService;
            _logger = logger;
            _context = context;
        }

        /// <summary>
        /// Search for venues near a location
        /// </summary>
        [HttpPost("venues/search")]
        [ProducesResponseType(typeof(List<VenueSearchResultDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> SearchVenues([FromBody] VenueSearchRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation(
                    "Venue search request: Lat={Latitude}, Lon={Longitude}, Radius={Radius}km, Theme={Theme}",
                    request.Latitude, request.Longitude, request.RadiusKm, request.Theme);

                var results = await _venueService.SearchNearbyAsync(request);

                return Ok(new
                {
                    success = true,
                    count = results.Count,
                    data = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while searching venues");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while searching for venues. Please try again later."
                });
            }
        }

        /// <summary>
        /// Simple text search for venues by name or city
        /// </summary>
        [HttpGet("venues/search")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> SearchVenuesByText([FromQuery] string q, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                {
                    return Ok(new List<object>());
                }

                var results = await _venueService.SearchByTextAsync(q, limit);

                return Ok(results.Select(venue => new
                {
                    venueId = venue.VenueId,
                    name = venue.Name,
                    city = venue.City,
                    country = venue.Country,
                    localImagePath = venue.LocalImagePath,
                    slug = venue.Slug,
                    roomCount = venue.Rooms?.Count ?? 0
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching venues by text: {Query}", q);
                return StatusCode(500, new List<object>());
            }
        }

        /// <summary>
        /// Get venue details by ID
        /// </summary>
        [HttpGet("venues/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVenue(int id)
        {
            try
            {
                var venue = await _venueService.GetByIdAsync(id);

                if (venue == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Venue not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = venue.VenueId,
                        name = venue.Name,
                        description = venue.Description,
                        address = venue.Address,
                        city = venue.City,
                        state = venue.State,
                        phone = venue.Phone,
                        website = venue.Website,
                        latitude = venue.Latitude,
                        longitude = venue.Longitude,
                        averageRating = venue.AverageRating,
                        totalReviews = venue.TotalReviews,
                        isOpenNow = venue.IsOpenNow(),
                        openingHours = venue.GetOpeningHours(),
                        priceRange = venue.PriceRange,
                        photos = venue.Photos?.Select(p => new { url = p.Url, caption = p.Caption }),
                        totalRooms = venue.Rooms?.Count ?? 0,
                        slug = venue.Slug,
                        rooms = venue.Rooms?.Select(r => new
                        {
                            roomId = r.RoomId,
                            name = r.Name,
                            theme = r.Theme,
                            difficulty = r.Difficulty,
                            minPlayers = r.MinPlayers,
                            maxPlayers = r.MaxPlayers,
                            durationMinutes = r.DurationMinutes,
                            successRate = r.SuccessRate,
                            averageRating = r.AverageRating
                        })
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting venue with ID {Id}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving venue details"
                });
            }
        }

        /// <summary>
        /// Get venue details by slug
        /// </summary>
        [HttpGet("venues/slug/{slug}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVenueBySlug(string slug)
        {
            try
            {
                var venue = await _venueService.GetBySlugAsync(slug);

                if (venue == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Venue not found"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = venue.VenueId,
                        name = venue.Name,
                        slug = venue.Slug,
                        address = venue.Address,
                        city = venue.City,
                        country = venue.Country,
                        latitude = venue.Latitude,
                        longitude = venue.Longitude,
                        googleMapsUrl = venue.GoogleMapsUrl,
                        coordinateInfo = new
                        {
                            storedLatitude = venue.Latitude,
                            storedLongitude = venue.Longitude,
                            isLatitudeValid = venue.Latitude >= -90 && venue.Latitude <= 90,
                            isLongitudeValid = venue.Longitude >= -180 && venue.Longitude <= 180,
                            googleMapsLink = $"https://www.google.com/maps?q={venue.Latitude},{venue.Longitude}"
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting venue with slug {Slug}", slug);
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving venue details"
                });
            }
        }

        /// <summary>
        /// Filter venues by country/city/theme
        /// </summary>
        [HttpGet("venues/filter")]
        [ProducesResponseType(typeof(List<VenueSearchResultDto>), 200)]
        public async Task<IActionResult> FilterVenues(
            [FromQuery] string? country = null,
            [FromQuery] string? city = null,
            [FromQuery] string? theme = null,
            [FromQuery] int? difficulty = null,
            [FromQuery] int? players = null,
            [FromQuery] double? minRating = null)
        {
            try
            {
                _logger.LogInformation(
                    "Venue filter request: Country={Country}, City={City}, Theme={Theme}, Difficulty={Difficulty}",
                    country, city, theme, difficulty);

                var results = await _venueService.FilterVenuesAsync(country, city, theme, difficulty, players, minRating);

                return Ok(new
                {
                    success = true,
                    count = results.Count,
                    data = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while filtering venues");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while filtering venues. Please try again later."
                });
            }
        }

        /// <summary>
        /// Get escape room details by ID
        /// </summary>
        [HttpGet("rooms/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetRoom(int id)
        {
            try
            {
                var room = await _context.Rooms
                    .Include(r => r.Venue)
                    .Include(r => r.Photos)
                    .Include(r => r.Reviews)
                    .FirstOrDefaultAsync(r => r.RoomId == id);

                if (room == null)
                {
                    return NotFound(new { success = false, message = "Room not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        roomId = room.RoomId,
                        name = room.Name,
                        description = room.Description,
                        theme = room.Theme,
                        themeDescription = room.ThemeDescription,
                        difficulty = room.Difficulty,
                        minPlayers = room.MinPlayers,
                        maxPlayers = room.MaxPlayers,
                        recommendedPlayers = room.RecommendedPlayers,
                        durationMinutes = room.DurationMinutes,
                        pricePerPerson = room.PricePerPerson,
                        pricePerGroup = room.PricePerGroup,
                        successRate = room.SuccessRate,
                        averageRating = room.AverageRating,
                        totalReviews = room.TotalReviews,
                        isScaryOrIntense = room.IsScaryOrIntense,
                        hasJumpscares = room.HasJumpscares,
                        requiresPhysicalActivity = room.RequiresPhysicalActivity,
                        isWheelchairAccessible = room.IsWheelchairAccessible,
                        isKidFriendly = room.IsKidFriendly,
                        hasActors = room.HasActors,
                        usesVR = room.UsesVR,
                        minAge = room.MinAge,
                        bookingUrl = room.BookingUrl,
                        photos = room.Photos?.Select(p => new { url = p.Url, caption = p.Caption }),
                        venue = new
                        {
                            venueId = room.Venue?.VenueId,
                            name = room.Venue?.Name,
                            city = room.Venue?.City,
                            slug = room.Venue?.Slug
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting room with ID {Id}", id);
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Get popular cities with venue counts
        /// </summary>
        [HttpGet("cities")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetCities()
        {
            try
            {
                var cities = await _context.Venues
                    .Where(v => v.IsActive)
                    .GroupBy(v => new { v.City, v.State, v.Country })
                    .Select(g => new
                    {
                        city = g.Key.City,
                        state = g.Key.State,
                        country = g.Key.Country,
                        count = g.Count()
                    })
                    .OrderByDescending(c => c.count)
                    .Take(20)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = cities
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cities");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving cities"
                });
            }
        }

        /// <summary>
        /// Get popular themes
        /// </summary>
        [HttpGet("themes")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetThemes()
        {
            try
            {
                var themes = await _context.Rooms
                    .Where(r => r.IsActive && !string.IsNullOrEmpty(r.Theme))
                    .GroupBy(r => r.Theme)
                    .Select(g => new
                    {
                        theme = g.Key,
                        count = g.Count()
                    })
                    .OrderByDescending(t => t.count)
                    .Take(20)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = themes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting themes");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving themes"
                });
            }
        }

        /// <summary>
        /// Track affiliate/booking link click and redirect
        /// </summary>
        [HttpGet("affiliate/click/{roomId}")]
        public async Task<IActionResult> TrackAffiliateClick(int roomId, [FromQuery] int? venueId = null, [FromQuery] string? source = null)
        {
            try
            {
                var room = await _context.Rooms.Include(r => r.Venue).FirstOrDefaultAsync(r => r.RoomId == roomId);
                if (room == null)
                {
                    return NotFound(new { success = false, message = "Room not found" });
                }

                var bookingUrl = room.BookingUrl ?? room.Venue?.Website ?? room.Venue?.BookingUrl;

                if (string.IsNullOrEmpty(bookingUrl))
                {
                    return NotFound(new { success = false, message = "No booking URL found for this room" });
                }

                // Record the click
                var click = new AffiliateClick
                {
                    RoomId = roomId,
                    VenueId = venueId ?? room.VenueId,
                    LinkType = source ?? "booking",
                    DestinationUrl = bookingUrl,
                    ReferrerPage = Request.Headers.Referer.ToString().Length > 100
                        ? Request.Headers.Referer.ToString()[..100]
                        : Request.Headers.Referer.ToString(),
                    IpAddress = GetClientIpAddress(),
                    UserAgent = Request.Headers.UserAgent.ToString().Length > 500
                        ? Request.Headers.UserAgent.ToString()[..500]
                        : Request.Headers.UserAgent.ToString(),
                    ClickedAt = DateTime.UtcNow
                };

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    click.UserId = userId;
                }

                _context.AffiliateClicks.Add(click);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Affiliate click tracked: RoomId={RoomId}, VenueId={VenueId}, Source={Source}", roomId, venueId, source ?? "booking");

                return Redirect(bookingUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking affiliate click for room {RoomId}", roomId);

                var room = await _context.Rooms.Include(r => r.Venue).FirstOrDefaultAsync(r => r.RoomId == roomId);
                if (room != null)
                {
                    var fallbackUrl = room.BookingUrl ?? room.Venue?.Website;
                    if (!string.IsNullOrEmpty(fallbackUrl))
                    {
                        return Redirect(fallbackUrl);
                    }
                }

                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        private string? GetClientIpAddress()
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',').FirstOrDefault()?.Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
