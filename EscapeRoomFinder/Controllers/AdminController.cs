using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVenueService _venueService;
        private readonly IBlogService _blogService;
        private readonly IGoogleMapsCrawlerService _crawlerService;
        private readonly IVenueWebsiteCrawlerService _websiteCrawlerService;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AdminController(
            ApplicationDbContext context,
            IVenueService venueService,
            IBlogService blogService,
            IGoogleMapsCrawlerService crawlerService,
            IVenueWebsiteCrawlerService websiteCrawlerService,
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            ILogger<AdminController> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _venueService = venueService;
            _blogService = blogService;
            _crawlerService = crawlerService;
            _websiteCrawlerService = websiteCrawlerService;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _environment = environment;
        }

        [Route("")]
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalVenues = await _context.Venues.CountAsync(v => v.IsActive);
            ViewBag.TotalRooms = await _context.Rooms.CountAsync(r => r.IsActive);
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalReviews = await _context.Reviews.CountAsync();
            ViewBag.PendingReviews = await _context.Reviews.CountAsync(r => !r.IsApproved);
            ViewBag.TotalBlogPosts = await _context.BlogPosts.CountAsync();
            ViewBag.PremiumListings = await _context.PremiumListings.CountAsync(p => p.IsActive);

            // Recent activity
            ViewBag.RecentVenues = await _context.Venues
                .OrderByDescending(v => v.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentReviews = await _context.Reviews
                .Include(r => r.Venue)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        [Route("venues")]
        public async Task<IActionResult> Venues(string? search, int page = 1)
        {
            var query = _context.Venues.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(v => v.Name.Contains(search) || v.City.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var venues = await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

            return View(venues);
        }

        [Route("venues/create")]
        public IActionResult CreateVenue()
        {
            return View(new EscapeRoomVenue());
        }

        [HttpPost]
        [Route("venues/create")]
        public async Task<IActionResult> CreateVenue(EscapeRoomVenue venue)
        {
            if (ModelState.IsValid)
            {
                venue = await _venueService.CreateVenueAsync(venue);
                TempData["Success"] = "Venue created successfully.";
                return RedirectToAction(nameof(Venues));
            }

            return View(venue);
        }

        [Route("venues/edit/{id}")]
        public async Task<IActionResult> EditVenue(int id)
        {
            var venue = await _context.Venues
                .Include(v => v.Rooms)
                .FirstOrDefaultAsync(v => v.VenueId == id);

            if (venue == null)
                return NotFound();

            return View(venue);
        }

        [HttpPost]
        [Route("venues/edit/{id}")]
        public async Task<IActionResult> EditVenue(int id, EscapeRoomVenue venue)
        {
            if (id != venue.VenueId)
                return NotFound();

            if (ModelState.IsValid)
            {
                await _venueService.UpdateVenueAsync(venue);
                TempData["Success"] = "Venue updated successfully.";
                return RedirectToAction(nameof(Venues));
            }

            return View(venue);
        }

        [Route("rooms")]
        public async Task<IActionResult> Rooms(int? venueId, int page = 1)
        {
            var query = _context.Rooms.Include(r => r.Venue).AsQueryable();

            if (venueId.HasValue)
            {
                query = query.Where(r => r.VenueId == venueId.Value);
            }

            var totalCount = await query.CountAsync();
            var rooms = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.VenueId = venueId;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);
            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();

            return View(rooms);
        }

        [Route("reviews")]
        public async Task<IActionResult> Reviews(bool? pending, int page = 1)
        {
            var query = _context.Reviews
                .Include(r => r.Venue)
                .Include(r => r.User)
                .AsQueryable();

            if (pending == true)
            {
                query = query.Where(r => !r.IsApproved);
            }

            var totalCount = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Pending = pending;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

            return View(reviews);
        }

        [HttpPost]
        [Route("reviews/approve/{id}")]
        public async Task<IActionResult> ApproveReview(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.ReviewId == id);

            if (review == null)
            {
                TempData["Error"] = "Review not found.";
                return RedirectToAction(nameof(Reviews));
            }

            review.IsApproved = true;
            review.ApprovedAt = DateTime.UtcNow;

            // Get admin user ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int adminUserId))
            {
                review.ApprovedByUserId = adminUserId;
            }

            await _context.SaveChangesAsync();
            await _venueService.UpdateVenueRatingAsync(review.VenueId);

            TempData["Success"] = "Review approved successfully.";
            return RedirectToAction(nameof(Reviews), new { pending = true });
        }

        [HttpPost]
        [Route("reviews/reject/{id}")]
        public async Task<IActionResult> RejectReview(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.Venue)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReviewId == id);

            if (review == null)
            {
                TempData["Error"] = "Review not found.";
                return RedirectToAction(nameof(Reviews));
            }

            var venue = review.Venue;
            var wasApproved = review.IsApproved;

            // Update user's TotalReviews if review belongs to a user
            if (review.User != null)
            {
                review.User.TotalReviews = Math.Max(0, review.User.TotalReviews - 1);
            }

            _context.Reviews.Remove(review);

            // Update venue rating if the deleted review was approved
            if (wasApproved && venue != null)
            {
                var remainingRatings = await _context.Reviews
                    .Where(r => r.VenueId == venue.VenueId && r.ReviewId != id && r.IsApproved)
                    .Select(r => r.Rating)
                    .ToListAsync();

                if (remainingRatings.Any())
                {
                    venue.AverageRating = (decimal)remainingRatings.Average();
                    venue.TotalReviews = remainingRatings.Count;
                }
                else
                {
                    venue.AverageRating = null;
                    venue.TotalReviews = 0;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Review rejected and deleted.";
            return RedirectToAction(nameof(Reviews));
        }

        [HttpPost]
        [Route("reviews/bulk-approve")]
        public async Task<IActionResult> BulkApproveReviews(int[] reviewIds)
        {
            if (reviewIds == null || reviewIds.Length == 0)
            {
                TempData["Error"] = "No reviews selected.";
                return RedirectToAction(nameof(Reviews));
            }

            var reviews = await _context.Reviews
                .Include(r => r.Venue)
                .Where(r => reviewIds.Contains(r.ReviewId) && !r.IsApproved)
                .ToListAsync();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            int? adminUserId = null;
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int adminId))
            {
                adminUserId = adminId;
            }

            var affectedVenueIds = new HashSet<int>();

            foreach (var review in reviews)
            {
                review.IsApproved = true;
                review.ApprovedAt = DateTime.UtcNow;
                review.ApprovedByUserId = adminUserId;
                affectedVenueIds.Add(review.VenueId);
            }

            await _context.SaveChangesAsync();

            // Update ratings for affected venues
            foreach (var venueId in affectedVenueIds)
            {
                await _venueService.UpdateVenueRatingAsync(venueId);
            }

            TempData["Success"] = $"Approved {reviews.Count} reviews.";
            return RedirectToAction(nameof(Reviews));
        }

        [Route("blog")]
        public async Task<IActionResult> BlogPosts(int page = 1)
        {
            var totalCount = await _context.BlogPosts.CountAsync();
            var posts = await _context.BlogPosts
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * 20)
                .Take(20)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

            // Get cities for auto-generate feature
            var cities = await _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.City))
                .GroupBy(v => v.City)
                .Select(g => new { City = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(50)
                .ToListAsync();
            ViewBag.Cities = cities;

            return View(posts);
        }

        [Route("blog/create")]
        public IActionResult CreateBlogPost()
        {
            return View(new BlogPost());
        }

        [HttpPost]
        [Route("blog/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogPost(BlogPost post)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            await _blogService.CreatePostAsync(post);
            TempData["Success"] = "Blog post created successfully.";
            return RedirectToAction(nameof(BlogPosts));
        }

        [Route("blog/edit/{id}")]
        public async Task<IActionResult> EditBlogPost(int id)
        {
            var post = await _blogService.GetPostByIdAsync(id);
            if (post == null)
            {
                return NotFound();
            }
            return View(post);
        }

        [HttpPost]
        [Route("blog/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlogPost(BlogPost post)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            await _blogService.UpdatePostAsync(post);
            TempData["Success"] = "Blog post updated successfully.";
            return RedirectToAction(nameof(BlogPosts));
        }

        [HttpPost]
        [Route("blog/delete/{id}")]
        public async Task<IActionResult> DeleteBlogPost(int id)
        {
            var result = await _blogService.DeletePostAsync(id);
            if (result)
            {
                TempData["Success"] = "Blog post deleted successfully.";
            }
            else
            {
                TempData["Error"] = "Blog post not found.";
            }
            return RedirectToAction(nameof(BlogPosts));
        }

        [HttpPost]
        [Route("blog/toggle-publish/{id}")]
        public async Task<IActionResult> ToggleBlogPostPublish(int id)
        {
            var post = await _blogService.GetPostByIdAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            post.IsPublished = !post.IsPublished;
            if (post.IsPublished && !post.PublishedAt.HasValue)
            {
                post.PublishedAt = DateTime.UtcNow;
            }

            await _blogService.UpdatePostAsync(post);

            TempData["Success"] = post.IsPublished
                ? "Blog post published."
                : "Blog post unpublished.";

            return RedirectToAction(nameof(BlogPosts));
        }

        [HttpPost]
        [Route("blog/generate-top-rooms")]
        public async Task<IActionResult> GenerateTopRoomsPosts([FromBody] GeneratePostsRequest request)
        {
            if (request.Cities == null || !request.Cities.Any())
            {
                return Json(new { success = false, message = "No cities selected." });
            }

            try
            {
                int postsCreated = 0;
                foreach (var city in request.Cities)
                {
                    // Get top-rated rooms in this city
                    var rooms = await _context.Rooms
                        .Include(r => r.Venue)
                        .Where(r => r.IsActive && r.Venue.IsActive && r.Venue.City == city)
                        .OrderByDescending(r => r.AverageRating ?? 0)
                        .ThenByDescending(r => r.TotalReviews)
                        .Take(10)
                        .ToListAsync();

                    if (rooms.Count < 3) continue;

                    var content = $"<p>Looking for the best escape room experiences in {city}? Here are the top-rated escape rooms based on player reviews:</p>\n\n";

                    for (int i = 0; i < rooms.Count; i++)
                    {
                        var room = rooms[i];
                        content += $"<h3>{i + 1}. {room.Name} at {room.Venue?.Name}</h3>\n";
                        content += $"<p><strong>Theme:</strong> {room.Theme} | <strong>Difficulty:</strong> {room.Difficulty}/5 | ";
                        content += $"<strong>Players:</strong> {room.MinPlayers}-{room.MaxPlayers}</p>\n";
                        if (!string.IsNullOrEmpty(room.Description))
                        {
                            content += $"<p>{room.Description}</p>\n";
                        }
                        content += "\n";
                    }

                    var post = new BlogPost
                    {
                        Title = $"Top {rooms.Count} Escape Rooms in {city}",
                        Slug = GenerateBlogSlug($"top-escape-rooms-{city}"),
                        Summary = $"Discover the best escape rooms in {city}. Our guide features the top-rated rooms based on player reviews and ratings.",
                        Content = content,
                        FeaturedImage = rooms.FirstOrDefault()?.LocalImagePath,
                        IsPublished = true,
                        PublishedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.BlogPosts.Add(post);
                    postsCreated++;
                }

                await _context.SaveChangesAsync();

                return Json(new {
                    success = true,
                    message = $"Generated {postsCreated} posts successfully.",
                    count = postsCreated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating top rooms posts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("blog/generate-city-guide")]
        public async Task<IActionResult> GenerateCityGuidePost([FromBody] GenerateCityGuideRequest request)
        {
            if (string.IsNullOrEmpty(request.City))
            {
                return Json(new { success = false, message = "City is required." });
            }

            try
            {
                var venues = await _context.Venues
                    .Include(v => v.Rooms)
                    .Where(v => v.IsActive && v.City == request.City)
                    .OrderByDescending(v => v.IsPremium)
                    .ThenByDescending(v => v.AverageRating ?? 0)
                    .ToListAsync();

                if (!venues.Any())
                {
                    return Json(new { success = false, message = $"No venues found in {request.City}." });
                }

                var content = $"<p>Planning an escape room adventure in {request.City}? This comprehensive guide covers everything you need to know about the escape room scene in the area.</p>\n\n";
                content += $"<h2>Overview</h2>\n";
                content += $"<p>{request.City} is home to {venues.Count} escape room venues with a total of {venues.Sum(v => v.TotalRooms)} rooms. ";
                content += $"Whether you're a beginner or an experienced puzzler, there's something for everyone.</p>\n\n";

                content += $"<h2>Top Venues</h2>\n";
                foreach (var venue in venues.Take(5))
                {
                    content += $"<h3>{venue.Name}</h3>\n";
                    content += $"<p><strong>Address:</strong> {venue.Address}</p>\n";
                    if (venue.AverageRating.HasValue)
                    {
                        content += $"<p><strong>Rating:</strong> {venue.AverageRating:F1}/5 ({venue.TotalReviews} reviews)</p>\n";
                    }
                    content += $"<p><strong>Rooms:</strong> {venue.TotalRooms} escape rooms available</p>\n\n";
                }

                var themes = venues.SelectMany(v => v.Rooms).Select(r => r.Theme).Distinct().Take(10);
                content += $"<h2>Popular Themes</h2>\n";
                content += $"<p>Escape rooms in {request.City} feature various themes including: {string.Join(", ", themes)}.</p>\n";

                var post = new BlogPost
                {
                    Title = $"Complete Guide to Escape Rooms in {request.City}",
                    Slug = GenerateBlogSlug($"escape-room-guide-{request.City}"),
                    Summary = $"Your ultimate guide to escape rooms in {request.City}. Find venues, compare rooms, and plan your next adventure.",
                    Content = content,
                    IsPublished = true,
                    PublishedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.BlogPosts.Add(post);
                await _context.SaveChangesAsync();

                return Json(new {
                    success = true,
                    message = $"Generated city guide for {request.City}.",
                    postId = post.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating city guide for {City}", request.City);
                return Json(new { success = false, message = ex.Message });
            }
        }

        private string GenerateBlogSlug(string title)
        {
            var slug = title.ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            var baseSlug = slug;
            var counter = 1;
            while (_context.BlogPosts.Any(p => p.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        [Route("crawl")]
        public IActionResult Crawl()
        {
            return View();
        }

        // DEBUG: Test endpoint without auth - remove in production
        [AllowAnonymous]
        [HttpGet]
        [Route("crawl/test")]
        public async Task<IActionResult> CrawlTest(string location = "Los Angeles", int maxResults = 3)
        {
            var logs = new List<string>();
            var startTime = DateTime.Now;

            try
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] TEST CRAWL - Starting for: {location}");
                _logger.LogInformation("========== TEST CRAWL START ==========");
                _logger.LogInformation("Location: {Location}, Max Results: {MaxResults}", location, maxResults);

                var results = await _crawlerService.CrawlEscapeRoomsAsync(location, maxResults);

                logs.Add($"[{DateTime.Now:HH:mm:ss}] Crawl found {results.Count} venues");
                foreach (var venue in results)
                {
                    logs.Add($"  - {venue.Name} | {venue.Address} | Rating: {venue.Rating}");
                }

                var elapsed = DateTime.Now - startTime;
                logs.Add($"[{DateTime.Now:HH:mm:ss}] Completed in {elapsed.TotalSeconds:F1}s");

                return Json(new
                {
                    success = true,
                    found = results.Count,
                    venues = results.Select(v => new { v.Name, v.Address, v.Rating, v.ReviewCount, v.Website }),
                    logs = logs,
                    elapsedSeconds = elapsed.TotalSeconds
                });
            }
            catch (Exception ex)
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                _logger.LogError(ex, "Test crawl failed");
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    logs = logs
                });
            }
        }

        [HttpPost]
        [Route("crawl/google-maps")]
        public async Task<IActionResult> CrawlGoogleMaps(string location, int maxResults = 20, bool ajax = false)
        {
            var logs = new List<string>();
            var startTime = DateTime.Now;

            try
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] Starting crawl for: {location}");
                _logger.LogInformation("========== CRAWL START ==========");
                _logger.LogInformation("Location: {Location}, Max Results: {MaxResults}", location, maxResults);

                logs.Add($"[{DateTime.Now:HH:mm:ss}] Initializing Playwright browser...");
                _logger.LogInformation("Initializing Playwright browser...");

                var results = await _crawlerService.CrawlEscapeRoomsAsync(location, maxResults);

                logs.Add($"[{DateTime.Now:HH:mm:ss}] Crawl completed. Found {results.Count} venues from Google Maps.");
                _logger.LogInformation("Crawl returned {Count} venues", results.Count);

                var savedCount = 0;
                var skippedCount = 0;
                var savedVenues = new List<string>();

                foreach (var venueData in results)
                {
                    _logger.LogInformation("Processing venue: {Name} at {Address}", venueData.Name, venueData.Address);

                    // Check if venue already exists
                    var existing = await _context.Venues
                        .FirstOrDefaultAsync(v => v.GooglePlaceId == venueData.GooglePlaceId ||
                                                  (v.Name == venueData.Name && v.Address == venueData.Address));

                    if (existing == null)
                    {
                        var city = ExtractCity(venueData.Address);
                        var venue = new EscapeRoomVenue
                        {
                            Name = venueData.Name,
                            Address = venueData.Address,
                            City = city,
                            Country = DetectCountry(venueData.Address, location),
                            Latitude = venueData.Latitude ?? 0,
                            Longitude = venueData.Longitude ?? 0,
                            Phone = venueData.Phone,
                            Website = venueData.Website,
                            GooglePlaceId = venueData.GooglePlaceId,
                            GoogleMapsUrl = venueData.GoogleMapsUrl,
                            LocalImagePath = venueData.LocalImagePath,
                            AverageRating = venueData.Rating.HasValue ? (decimal)venueData.Rating.Value : null,
                            TotalReviews = venueData.ReviewCount ?? 0,
                            Slug = GenerateSlug(venueData.Name, city),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Venues.Add(venue);
                        await _context.SaveChangesAsync();

                        logs.Add($"[{DateTime.Now:HH:mm:ss}] ✓ Saved: {venueData.Name} ({city})");
                        _logger.LogInformation("✓ Saved venue: {Name} (ID: {Id})", venue.Name, venue.VenueId);

                        // Save reviews
                        if (venueData.Reviews.Any())
                        {
                            await _crawlerService.SaveCrawledReviewsAsync(venue.VenueId, venueData.Reviews);
                            logs.Add($"[{DateTime.Now:HH:mm:ss}]   - Imported {venueData.Reviews.Count} reviews");
                            _logger.LogInformation("  - Imported {Count} reviews", venueData.Reviews.Count);
                        }

                        savedCount++;
                        savedVenues.Add(venueData.Name);
                    }
                    else
                    {
                        logs.Add($"[{DateTime.Now:HH:mm:ss}] ⊘ Skipped (exists): {venueData.Name}");
                        _logger.LogInformation("⊘ Skipped (already exists): {Name}", venueData.Name);
                        skippedCount++;
                    }
                }

                var elapsed = DateTime.Now - startTime;
                var summary = $"Crawl completed in {elapsed.TotalSeconds:F1}s. Found: {results.Count}, Saved: {savedCount}, Skipped: {skippedCount}";
                logs.Add($"[{DateTime.Now:HH:mm:ss}] {summary}");
                _logger.LogInformation("========== CRAWL END: {Summary} ==========", summary);

                if (ajax)
                {
                    return Json(new
                    {
                        success = true,
                        message = summary,
                        found = results.Count,
                        saved = savedCount,
                        skipped = skippedCount,
                        venues = savedVenues,
                        logs = logs,
                        elapsedSeconds = elapsed.TotalSeconds
                    });
                }

                TempData["Success"] = summary;
                TempData["CrawlLogs"] = string.Join("\n", logs);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Crawl failed: {ex.Message}";
                logs.Add($"[{DateTime.Now:HH:mm:ss}] ✗ ERROR: {ex.Message}");
                _logger.LogError(ex, "========== CRAWL ERROR ==========");
                _logger.LogError("Error details: {Message}", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {Inner}", ex.InnerException.Message);
                    logs.Add($"[{DateTime.Now:HH:mm:ss}] Inner error: {ex.InnerException.Message}");
                }

                if (ajax)
                {
                    return Json(new
                    {
                        success = false,
                        message = errorMsg,
                        error = ex.Message,
                        innerError = ex.InnerException?.Message,
                        logs = logs
                    });
                }

                TempData["Error"] = errorMsg;
                TempData["CrawlLogs"] = string.Join("\n", logs);
            }

            return RedirectToAction(nameof(Crawl));
        }

        private string DetectCountry(string address, string searchLocation)
        {
            var lowerAddress = address.ToLower();
            var lowerLocation = searchLocation.ToLower();

            // Check for common country indicators
            if (lowerAddress.Contains("uk") || lowerAddress.Contains("united kingdom") || lowerAddress.Contains("england") || lowerLocation.Contains("uk"))
                return "United Kingdom";
            if (lowerAddress.Contains("canada") || lowerLocation.Contains("canada"))
                return "Canada";
            if (lowerAddress.Contains("australia") || lowerLocation.Contains("australia"))
                return "Australia";
            if (lowerAddress.Contains("germany") || lowerAddress.Contains("deutschland") || lowerLocation.Contains("germany"))
                return "Germany";
            if (lowerAddress.Contains("france") || lowerLocation.Contains("france"))
                return "France";
            if (lowerAddress.Contains("spain") || lowerAddress.Contains("españa") || lowerLocation.Contains("spain"))
                return "Spain";
            if (lowerAddress.Contains("italy") || lowerAddress.Contains("italia") || lowerLocation.Contains("italy"))
                return "Italy";
            if (lowerAddress.Contains("netherlands") || lowerLocation.Contains("netherlands") || lowerLocation.Contains("amsterdam"))
                return "Netherlands";
            if (lowerAddress.Contains("japan") || lowerLocation.Contains("japan") || lowerLocation.Contains("tokyo"))
                return "Japan";
            if (lowerAddress.Contains("singapore") || lowerLocation.Contains("singapore"))
                return "Singapore";

            // Default to USA for US state abbreviations
            var usStates = new[] { "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY", "DC" };
            foreach (var state in usStates)
            {
                if (address.Contains($", {state}") || address.Contains($" {state} ") || address.EndsWith($" {state}"))
                    return "United States";
            }

            return "United States"; // Default
        }

        [HttpPost]
        [Route("crawl/venue-website/{id}")]
        public async Task<IActionResult> CrawlVenueWebsite(int id, bool ajax = false)
        {
            try
            {
                var venue = await _context.Venues.FindAsync(id);
                if (venue == null || string.IsNullOrEmpty(venue.Website))
                {
                    if (ajax)
                        return Json(new { success = false, message = "Venue not found or has no website." });

                    TempData["Error"] = "Venue not found or has no website.";
                    return RedirectToAction(nameof(Venues));
                }

                _logger.LogInformation("Starting room crawl for venue {Name} from {Website}", venue.Name, venue.Website);
                var rooms = await _websiteCrawlerService.CrawlVenueWebsiteForRoomsAsync(venue.Website);
                _logger.LogInformation("Room crawl found {Count} potential rooms", rooms.Count);

                var savedCount = 0;
                var savedRooms = new List<string>();

                foreach (var roomData in rooms)
                {
                    // Check if room already exists
                    var existing = await _context.Rooms
                        .FirstOrDefaultAsync(r => r.VenueId == id && r.Name == roomData.Name);

                    if (existing == null)
                    {
                        var room = new EscapeRoom
                        {
                            VenueId = id,
                            Name = roomData.Name,
                            Description = roomData.Description,
                            Theme = roomData.Theme ?? "Mystery",
                            Difficulty = roomData.Difficulty ?? 3,
                            MinPlayers = roomData.MinPlayers ?? 2,
                            MaxPlayers = roomData.MaxPlayers ?? 6,
                            DurationMinutes = roomData.DurationMinutes ?? 60,
                            PricePerPerson = roomData.Price,
                            ImageUrl = roomData.ImageUrl,
                            Slug = GenerateRoomSlug(roomData.Name, venue.VenueId),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Rooms.Add(room);
                        savedCount++;
                        savedRooms.Add(roomData.Name);
                        _logger.LogInformation("  ✓ Saved room: {Name}", roomData.Name);
                    }
                    else
                    {
                        _logger.LogInformation("  ⊘ Room already exists: {Name}", roomData.Name);
                    }
                }

                // Update venue room count
                venue.TotalRooms = await _context.Rooms.CountAsync(r => r.VenueId == id && r.IsActive);
                await _context.SaveChangesAsync();

                var message = $"Crawl completed. Found {rooms.Count} rooms, saved {savedCount} new rooms.";
                _logger.LogInformation(message);

                if (ajax)
                {
                    return Json(new
                    {
                        success = true,
                        found = rooms.Count,
                        saved = savedCount,
                        rooms = savedRooms,
                        message
                    });
                }

                TempData["Success"] = message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during website crawl for venue {Id}", id);

                if (ajax)
                    return Json(new { success = false, message = ex.Message });

                TempData["Error"] = $"Crawl failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Venues));
        }

        [HttpGet]
        [Route("api/venues-without-rooms")]
        public async Task<IActionResult> GetVenuesWithoutRooms()
        {
            var venues = await _context.Venues
                .Where(v => v.IsActive && !string.IsNullOrEmpty(v.Website))
                .Select(v => new
                {
                    id = v.VenueId,
                    name = v.Name,
                    city = v.City,
                    website = v.Website,
                    roomCount = v.Rooms.Count(r => r.IsActive)
                })
                .OrderBy(v => v.roomCount)
                .ThenBy(v => v.name)
                .Take(100)
                .ToListAsync();

            return Json(venues);
        }

        /// <summary>
        /// Clear all venue data from database for testing
        /// POST: /admin/crawl/clear-all
        /// </summary>
        [HttpPost]
        [Route("crawl/clear-all")]
        public async Task<IActionResult> ClearAllData()
        {
            try
            {
                // Get counts before deletion
                var venueCount = await _context.Venues.CountAsync();
                var roomCount = await _context.Rooms.CountAsync();
                var reviewCount = await _context.Reviews.CountAsync();
                var photoCount = await _context.Photos.CountAsync();

                // Delete in order (related tables first due to FK constraints)
                _context.Reviews.RemoveRange(_context.Reviews);
                _context.Photos.RemoveRange(_context.Photos);
                _context.Rooms.RemoveRange(_context.Rooms);
                _context.PremiumListings.RemoveRange(_context.PremiumListings);
                _context.Venues.RemoveRange(_context.Venues);

                await _context.SaveChangesAsync();

                // Delete all images from wwwroot/images folders
                int imagesDeleted = 0;
                var imageFolders = new[] { "venues", "photos", "rooms" };
                foreach (var folder in imageFolders)
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, "images", folder);
                    if (Directory.Exists(imagePath))
                    {
                        var imageFiles = Directory.GetFiles(imagePath);
                        foreach (var file in imageFiles)
                        {
                            try
                            {
                                System.IO.File.Delete(file);
                                imagesDeleted++;
                            }
                            catch (Exception fileEx)
                            {
                                _logger.LogWarning(fileEx, "Failed to delete image file: {File}", file);
                            }
                        }
                        _logger.LogInformation("Deleted {Count} image files from {Path}", imageFiles.Length, imagePath);
                    }
                }

                _logger.LogInformation("Cleared all data: {VenueCount} venues, {RoomCount} rooms, {ReviewCount} reviews, {PhotoCount} photos, {ImagesDeleted} image files",
                    venueCount, roomCount, reviewCount, photoCount, imagesDeleted);

                return Json(new
                {
                    success = true,
                    message = "All data cleared successfully.",
                    deleted = new
                    {
                        venues = venueCount,
                        rooms = roomCount,
                        reviews = reviewCount,
                        photos = photoCount,
                        imageFiles = imagesDeleted
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all data");
                return Json(new
                {
                    success = false,
                    message = $"Error: {ex.Message}"
                });
            }
        }

        private string ExtractCity(string address)
        {
            // Simple city extraction - can be improved
            var parts = address.Split(',');
            if (parts.Length >= 2)
            {
                return parts[^2].Trim();
            }
            return "Unknown";
        }

        private string GenerateSlug(string name, string city)
        {
            var slug = $"{name}-{city}".ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            var baseSlug = slug;
            var counter = 1;
            while (_context.Venues.Any(v => v.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        private string GenerateRoomSlug(string name, int venueId)
        {
            var slug = name.ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            var baseSlug = slug;
            var counter = 1;
            while (_context.Rooms.Any(r => r.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        #region Room Management

        [Route("rooms/create")]
        public async Task<IActionResult> CreateRoom(int? venueId = null)
        {
            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
            ViewBag.PreselectedVenueId = venueId;
            return View(new EscapeRoom { VenueId = venueId ?? 0 });
        }

        [HttpPost]
        [Route("rooms/create")]
        public async Task<IActionResult> CreateRoom(EscapeRoom room)
        {
            if (ModelState.IsValid)
            {
                room.Slug = GenerateRoomSlug(room.Name, room.VenueId);
                room.CreatedAt = DateTime.UtcNow;
                room.UpdatedAt = DateTime.UtcNow;
                room.IsActive = true;

                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                // Update venue room count
                var venue = await _context.Venues.FindAsync(room.VenueId);
                if (venue != null)
                {
                    venue.TotalRooms = await _context.Rooms.CountAsync(r => r.VenueId == room.VenueId && r.IsActive);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Room created successfully.";
                return RedirectToAction(nameof(Rooms), new { venueId = room.VenueId });
            }

            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
            return View(room);
        }

        [Route("rooms/edit/{id}")]
        public async Task<IActionResult> EditRoom(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Venue)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
                return NotFound();

            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
            return View(room);
        }

        [HttpPost]
        [Route("rooms/edit/{id}")]
        public async Task<IActionResult> EditRoom(int id, EscapeRoom room)
        {
            if (id != room.RoomId)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existingRoom = await _context.Rooms.FindAsync(id);
                if (existingRoom == null)
                    return NotFound();

                existingRoom.Name = room.Name;
                existingRoom.Description = room.Description;
                existingRoom.Theme = room.Theme;
                existingRoom.Difficulty = room.Difficulty;
                existingRoom.MinPlayers = room.MinPlayers;
                existingRoom.MaxPlayers = room.MaxPlayers;
                existingRoom.DurationMinutes = room.DurationMinutes;
                existingRoom.PricePerPerson = room.PricePerPerson;
                existingRoom.IsKidFriendly = room.IsKidFriendly;
                existingRoom.IsActive = room.IsActive;
                existingRoom.BookingUrl = room.BookingUrl;
                existingRoom.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Room updated successfully.";
                return RedirectToAction(nameof(Rooms), new { venueId = existingRoom.VenueId });
            }

            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
            return View(room);
        }

        [HttpPost]
        [Route("rooms/delete/{id}")]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return Json(new { success = false, message = "Room not found." });
            }

            var venueId = room.VenueId;
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            // Update venue room count
            var venue = await _context.Venues.FindAsync(venueId);
            if (venue != null)
            {
                venue.TotalRooms = await _context.Rooms.CountAsync(r => r.VenueId == venueId && r.IsActive);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Room deleted successfully." });
        }

        [HttpPost]
        [Route("rooms/delete-selected")]
        public async Task<IActionResult> DeleteSelectedRooms(int[] roomIds)
        {
            if (roomIds == null || roomIds.Length == 0)
            {
                TempData["Error"] = "No rooms selected.";
                return RedirectToAction(nameof(Rooms));
            }

            var rooms = await _context.Rooms
                .Where(r => roomIds.Contains(r.RoomId))
                .ToListAsync();

            var affectedVenueIds = rooms.Select(r => r.VenueId).Distinct().ToList();

            _context.Rooms.RemoveRange(rooms);
            await _context.SaveChangesAsync();

            // Update venue room counts
            foreach (var venueId in affectedVenueIds)
            {
                var venue = await _context.Venues.FindAsync(venueId);
                if (venue != null)
                {
                    venue.TotalRooms = await _context.Rooms.CountAsync(r => r.VenueId == venueId && r.IsActive);
                }
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deleted {rooms.Count} rooms.";
            return RedirectToAction(nameof(Rooms));
        }

        #endregion

        #region User Management

        [Route("users")]
        public async Task<IActionResult> Users(string filter = "all", string? search = null, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .AsQueryable();

            ViewBag.Filter = filter;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;

            // Apply filter
            switch (filter.ToLower())
            {
                case "admin":
                    var adminRoleId = await _context.Roles
                        .Where(r => r.Name == "Admin")
                        .Select(r => r.Id)
                        .FirstOrDefaultAsync();
                    var adminUserIds = await _context.UserRoles
                        .Where(ur => ur.RoleId == adminRoleId)
                        .Select(ur => ur.UserId)
                        .ToListAsync();
                    query = query.Where(u => adminUserIds.Contains(u.Id));
                    break;
                case "venueowner":
                    query = query.Where(u => u.IsVenueOwner);
                    break;
                case "locked":
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                    break;
            }

            // Apply search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(u =>
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(searchLower)) ||
                    (u.DisplayName != null && u.DisplayName.ToLower().Contains(searchLower)));
            }

            // Get counts
            ViewBag.TotalCount = await _context.Users.CountAsync();
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            ViewBag.AdminCount = adminRole != null
                ? await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRole.Id)
                : 0;
            ViewBag.VenueOwnerCount = await _context.Users.CountAsync(u => u.IsVenueOwner);
            ViewBag.LockedCount = await _context.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);

            // Pagination
            var totalItems = await query.CountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get roles for each user
            var userRoles = new Dictionary<int, List<string>>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.ToList();
            }
            ViewBag.UserRoles = userRoles;

            // Get all available roles
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            return View(users);
        }

        [Route("users/edit/{id}")]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Reviews)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = roles.ToList();
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            ViewBag.Venues = await _context.Venues.OrderBy(v => v.Name).ToListAsync();

            return View(user);
        }

        [HttpPost]
        [Route("users/update/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(int id, string firstName, string lastName, string? displayName,
            string? city, string? country, bool isVenueOwner, int? venueId, string[] roles)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Update user properties
            user.FirstName = firstName;
            user.LastName = lastName;
            user.DisplayName = displayName ?? $"{firstName} {lastName}";
            user.City = city;
            user.Country = country;
            user.IsVenueOwner = isVenueOwner;
            user.VenueId = isVenueOwner ? venueId : null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(EditUser), new { id });
            }

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Except(roles ?? Array.Empty<string>());
            var rolesToAdd = (roles ?? Array.Empty<string>()).Except(currentRoles);

            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            await _userManager.AddToRolesAsync(user, rolesToAdd);

            TempData["Success"] = $"User '{user.Email}' updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Route("users/toggle-lockout/{id}")]
        public async Task<IActionResult> ToggleUserLockout(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Don't allow locking out yourself
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
            {
                TempData["Error"] = "You cannot lock out yourself.";
                return RedirectToAction(nameof(Users));
            }

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                // Unlock user
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["Success"] = $"User '{user.Email}' has been unlocked.";
            }
            else
            {
                // Lock user for 100 years (effectively permanent)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["Success"] = $"User '{user.Email}' has been locked out.";
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Route("users/delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Don't allow deleting yourself
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
            {
                TempData["Error"] = "You cannot delete yourself.";
                return RedirectToAction(nameof(Users));
            }

            // Check if user is an admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] = "Cannot delete admin users. Remove admin role first.";
                return RedirectToAction(nameof(Users));
            }

            var email = user.Email;

            // Delete user's reviews first
            var userReviews = await _context.Reviews.Where(r => r.UserId == id).ToListAsync();
            _context.Reviews.RemoveRange(userReviews);

            // Delete user
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = $"User '{email}' and {userReviews.Count} reviews have been deleted.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [Route("users/reset-password/{id}")]
        public async Task<IActionResult> ResetUserPassword(int id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters.";
                return RedirectToAction(nameof(EditUser), new { id });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Password for '{user.Email}' has been reset.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(EditUser), new { id });
        }

        [HttpPost]
        [Route("users/confirm-email/{id}")]
        public async Task<IActionResult> ConfirmUserEmail(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                TempData["Success"] = $"Email for '{user.Email}' has been confirmed.";
            }
            else
            {
                TempData["Error"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(EditUser), new { id });
        }

        #endregion

        #region Venue Management

        [HttpPost]
        [Route("venues/delete/{id}")]
        public async Task<IActionResult> DeleteVenue(int id)
        {
            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return Json(new { success = false, message = "Venue not found." });
            }

            venue.IsActive = false;
            venue.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Venue deleted successfully." });
        }

        [HttpPost]
        [Route("venues/toggle-premium/{id}")]
        public async Task<IActionResult> ToggleVenuePremium(int id)
        {
            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return Json(new { success = false, message = "Venue not found." });
            }

            venue.IsPremium = !venue.IsPremium;
            venue.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = venue.IsPremium ? "Venue marked as premium." : "Venue premium status removed." });
        }

        #endregion
    }

    public class GeneratePostsRequest
    {
        public List<string> Cities { get; set; } = new();
    }

    public class GenerateCityGuideRequest
    {
        public string City { get; set; } = string.Empty;
    }
}
