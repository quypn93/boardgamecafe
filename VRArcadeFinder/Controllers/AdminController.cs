using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("[controller]")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IArcadeService _arcadeService;
        private readonly IBlogService _blogService;
        private readonly IGoogleMapsCrawlerService _crawlerService;
        private readonly IAutoCrawlService _autoCrawlService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IArcadeService arcadeService,
            IBlogService blogService,
            IGoogleMapsCrawlerService crawlerService,
            IAutoCrawlService autoCrawlService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _arcadeService = arcadeService;
            _blogService = blogService;
            _crawlerService = crawlerService;
            _autoCrawlService = autoCrawlService;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var stats = new
            {
                TotalArcades = await _context.Arcades.CountAsync(a => a.IsActive),
                TotalGames = await _context.VRGames.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalReviews = await _context.Reviews.CountAsync(),
                PendingReviews = await _context.Reviews.CountAsync(r => !r.IsApproved),
                RecentArcades = await _context.Arcades
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .Select(a => new { a.ArcadeId, a.Name, a.City, a.CreatedAt })
                    .ToListAsync(),
                TopCities = await _context.Arcades
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.City)
                    .Select(g => new { City = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(10)
                    .ToListAsync()
            };

            return View(stats);
        }

        [HttpGet("Arcades")]
        public async Task<IActionResult> Arcades(string? search = null, string? city = null, string? status = null, string? premium = null, int page = 1, int pageSize = 20)
        {
            var query = _context.Arcades.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.Name.Contains(search) || a.Address.Contains(search));
            }

            if (!string.IsNullOrEmpty(city))
            {
                query = query.Where(a => a.City == city);
            }

            if (status == "active")
            {
                query = query.Where(a => a.IsActive);
            }
            else if (status == "inactive")
            {
                query = query.Where(a => !a.IsActive);
            }

            if (premium == "true")
            {
                query = query.Where(a => a.IsPremium);
            }
            else if (premium == "false")
            {
                query = query.Where(a => !a.IsPremium);
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.SelectedCity = city;
            ViewBag.Status = status;
            ViewBag.Premium = premium;
            ViewBag.Cities = await _context.Arcades
                .Where(a => a.City != null)
                .Select(a => a.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var arcades = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(arcades);
        }

        [HttpGet("Games")]
        public async Task<IActionResult> Games(string? search = null, string? platform = null, string? genre = null, string? multiplayer = null, int page = 1, int pageSize = 24)
        {
            var query = _context.VRGames.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(g => g.Name.Contains(search));
            }

            if (!string.IsNullOrEmpty(platform))
            {
                query = query.Where(g => g.VRPlatform == platform);
            }

            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(g => g.Genre == genre);
            }

            if (multiplayer == "true")
            {
                query = query.Where(g => g.IsMultiplayer);
            }
            else if (multiplayer == "false")
            {
                query = query.Where(g => !g.IsMultiplayer);
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Platform = platform;
            ViewBag.Genre = genre;
            ViewBag.Multiplayer = multiplayer;

            var games = await query
                .OrderBy(g => g.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(games);
        }

        [HttpGet("Reviews")]
        public async Task<IActionResult> Reviews(string? status = null, string? rating = null, string? search = null, int page = 1, int pageSize = 20)
        {
            var query = _context.Reviews
                .Include(r => r.Arcade)
                .Include(r => r.User)
                .AsQueryable();

            if (status == "pending")
            {
                query = query.Where(r => !r.IsApproved);
            }
            else if (status == "approved")
            {
                query = query.Where(r => r.IsApproved);
            }

            if (!string.IsNullOrEmpty(rating) && int.TryParse(rating, out int ratingValue))
            {
                query = query.Where(r => r.Rating == ratingValue);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r => r.Content.Contains(search) || (r.Title != null && r.Title.Contains(search)));
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Status = status;
            ViewBag.Rating = rating;
            ViewBag.Search = search;

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(reviews);
        }

        [HttpPost("reviews/approve/{id}")]
        public async Task<IActionResult> ApproveReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            review.ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("reviews/reject/{id}")]
        public async Task<IActionResult> RejectReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("reviews/delete/{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("Users")]
        public async Task<IActionResult> Users(string? search = null, string? role = null, string? status = null, int page = 1, int pageSize = 20)
        {
            var query = _context.Users.Include(u => u.Reviews).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => u.Email.Contains(search) ||
                    (u.DisplayName != null && u.DisplayName.Contains(search)));
            }

            if (role == "Admin")
            {
                // Would need to check roles properly
            }
            else if (role == "ArcadeOwner")
            {
                query = query.Where(u => u.IsArcadeOwner);
            }

            if (status == "locked")
            {
                query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.Now);
            }
            else if (status == "active")
            {
                query = query.Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTimeOffset.Now);
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.Status = status;

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(users);
        }

        [HttpPost("users/lock/{id}")]
        public async Task<IActionResult> LockUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("users/unlock/{id}")]
        public async Task<IActionResult> UnlockUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("users/delete/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("Blog")]
        public async Task<IActionResult> Blog(string? search = null, string? status = null, string? category = null, int page = 1, int pageSize = 20)
        {
            var query = _context.BlogPosts.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Title.Contains(search));
            }

            if (status == "published")
            {
                query = query.Where(p => p.IsPublished);
            }
            else if (status == "draft")
            {
                query = query.Where(p => !p.IsPublished);
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Category = category;

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(posts);
        }

        [HttpPost("blog/publish/{id}")]
        public async Task<IActionResult> PublishPost(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            post.IsPublished = true;
            post.PublishedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("blog/unpublish/{id}")]
        public async Task<IActionResult> UnpublishPost(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            post.IsPublished = false;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("blog/delete/{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("Cities")]
        public async Task<IActionResult> Cities()
        {
            var cities = await _context.Cities
                .OrderBy(c => c.Country)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return View(cities);
        }

        [HttpPost("cities/add")]
        public async Task<IActionResult> AddCity(string name, string country, double latitude, double longitude, int searchRadius = 50000, bool isPriority = false)
        {
            var city = new Models.Domain.City
            {
                Name = name,
                Country = country,
                Latitude = latitude,
                Longitude = longitude,
                SearchRadius = searchRadius,
                IsPriority = isPriority,
                IsEnabled = true
            };

            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Cities));
        }

        [HttpPost("cities/toggle/{id}")]
        public async Task<IActionResult> ToggleCity(int id, bool enable)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                return NotFound();
            }

            city.IsEnabled = enable;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("cities/delete/{id}")]
        public async Task<IActionResult> DeleteCity(int id)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                return NotFound();
            }

            _context.Cities.Remove(city);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("cities/crawl/{id}")]
        public async Task<IActionResult> CrawlCity(int id)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                return NotFound();
            }

            // Start crawl in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _autoCrawlService.CrawlCityAsync(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error crawling city {CityId}", id);
                }
            });

            return Ok();
        }

        [HttpPost("cities/crawl-all")]
        public async Task<IActionResult> CrawlAllCities()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _autoCrawlService.RunCrawlCycleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running crawl cycle");
                }
            });

            return Ok();
        }

        [HttpGet("Crawl")]
        public async Task<IActionResult> Crawl()
        {
            ViewBag.Cities = await _context.Cities
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CrawlHistory = await _context.CrawlHistories
                .Include(h => h.City)
                .OrderByDescending(h => h.StartedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.AutoCrawlEnabled = true; // TODO: Get from settings
            ViewBag.CrawlInterval = 24;
            ViewBag.MaxResults = 50;
            ViewBag.TotalCrawls = await _context.CrawlHistories.CountAsync();
            ViewBag.TotalFound = await _context.CrawlHistories.SumAsync(h => h.ArcadesFound);
            ViewBag.CitiesCrawled = await _context.Cities.CountAsync(c => c.LastCrawledAt.HasValue);

            return View();
        }

        [HttpPost("crawl/start")]
        public async Task<IActionResult> StartCrawl(int cityId, int radius = 50000, string? keywords = null)
        {
            var city = await _context.Cities.FindAsync(cityId);
            if (city == null)
            {
                return NotFound();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await _autoCrawlService.CrawlCityAsync(cityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting crawl for city {CityId}", cityId);
                }
            });

            return RedirectToAction(nameof(Crawl));
        }

        [HttpPost("crawl/start-ajax")]
        public async Task<IActionResult> StartCrawlAjax(int cityId, int radius = 50000, string? keywords = null)
        {
            var city = await _context.Cities.FindAsync(cityId);
            if (city == null)
            {
                return Json(new { success = false, error = "City not found" });
            }

            // Create crawl history record
            var crawlHistory = new CrawlHistory
            {
                CityId = cityId,
                StartedAt = DateTime.UtcNow,
                Status = "InProgress"
            };
            _context.CrawlHistories.Add(crawlHistory);
            await _context.SaveChangesAsync();

            var crawlId = crawlHistory.CrawlHistoryId;

            // Start crawl in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _autoCrawlService.CrawlCityAsync(cityId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting crawl for city {CityId}", cityId);
                }
            });

            return Json(new { success = true, crawlId = crawlId, message = $"Crawl started for {city.Name}" });
        }

        [HttpGet("crawl/status/{id}")]
        public async Task<IActionResult> GetCrawlStatus(int id)
        {
            var crawl = await _context.CrawlHistories
                .Include(h => h.City)
                .FirstOrDefaultAsync(h => h.CrawlHistoryId == id);

            if (crawl == null)
            {
                return Json(new { found = false });
            }

            return Json(new
            {
                found = true,
                id = crawl.CrawlHistoryId,
                city = crawl.City?.Name,
                status = crawl.Status,
                startedAt = crawl.StartedAt.ToString("HH:mm:ss"),
                completedAt = crawl.CompletedAt?.ToString("HH:mm:ss"),
                arcadesFound = crawl.ArcadesFound,
                arcadesAdded = crawl.ArcadesAdded,
                arcadesUpdated = crawl.ArcadesUpdated,
                errorMessage = crawl.ErrorMessage,
                isRunning = crawl.Status == "InProgress" || crawl.Status == "Running"
            });
        }

        [HttpGet("crawl/latest-status")]
        public async Task<IActionResult> GetLatestCrawlStatus()
        {
            var crawl = await _context.CrawlHistories
                .Include(h => h.City)
                .OrderByDescending(h => h.StartedAt)
                .FirstOrDefaultAsync();

            if (crawl == null)
            {
                return Json(new { found = false, isRunning = false });
            }

            return Json(new
            {
                found = true,
                id = crawl.CrawlHistoryId,
                city = crawl.City?.Name,
                status = crawl.Status,
                startedAt = crawl.StartedAt.ToString("HH:mm:ss"),
                completedAt = crawl.CompletedAt?.ToString("HH:mm:ss"),
                arcadesFound = crawl.ArcadesFound,
                arcadesAdded = crawl.ArcadesAdded,
                arcadesUpdated = crawl.ArcadesUpdated,
                errorMessage = crawl.ErrorMessage,
                isRunning = crawl.Status == "InProgress" || crawl.Status == "Running"
            });
        }

        [HttpGet("crawl/log/{id}")]
        public async Task<IActionResult> GetCrawlLog(int id)
        {
            var crawl = await _context.CrawlHistories
                .Include(h => h.City)
                .FirstOrDefaultAsync(h => h.CrawlHistoryId == id);

            if (crawl == null)
            {
                return NotFound();
            }

            var log = $"Crawl ID: {crawl.CrawlHistoryId}\n";
            log += $"City: {crawl.City?.Name}\n";
            log += $"Started: {crawl.StartedAt}\n";
            log += $"Completed: {crawl.CompletedAt}\n";
            log += $"Status: {crawl.Status}\n";
            log += $"Arcades Found: {crawl.ArcadesFound}\n";
            log += $"Arcades Added: {crawl.ArcadesAdded}\n";
            log += $"Arcades Updated: {crawl.ArcadesUpdated}\n";
            if (!string.IsNullOrEmpty(crawl.ErrorMessage))
            {
                log += $"\nError: {crawl.ErrorMessage}";
            }

            return Content(log, "text/plain");
        }

        [HttpPost("crawl/clear-all-data")]
        public async Task<IActionResult> ClearAllData()
        {
            try
            {
                // Delete all related data first (due to foreign keys)
                var reviewsDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM Reviews");
                var photosDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM Photos");
                var eventsDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM Events");
                var arcadeGamesDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM ArcadeGames");
                var premiumListingsDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM PremiumListings");
                var claimRequestsDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM ClaimRequests");
                var invoicesDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM Invoices");
                var affiliateClicksDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM AffiliateClicks");

                // Delete arcades
                var arcadesDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM Arcades");

                // Delete crawl history
                var historyDeleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM CrawlHistories");

                // Reset city crawl counts
                await _context.Database.ExecuteSqlRawAsync("UPDATE Cities SET CrawlCount = 0, LastCrawledAt = NULL");

                _logger.LogInformation("Cleared all data: {ArcadesDeleted} arcades, {HistoryDeleted} crawl history records", arcadesDeleted, historyDeleted);

                return Json(new { success = true, arcadesDeleted = arcadesDeleted, historyDeleted = historyDeleted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all data");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("crawl/clear-history")]
        public async Task<IActionResult> ClearCrawlHistory()
        {
            try
            {
                var deleted = await _context.Database.ExecuteSqlRawAsync("DELETE FROM CrawlHistories");

                // Reset city crawl counts
                await _context.Database.ExecuteSqlRawAsync("UPDATE Cities SET CrawlCount = 0, LastCrawledAt = NULL");

                _logger.LogInformation("Cleared crawl history: {Deleted} records", deleted);

                return Json(new { success = true, deleted = deleted });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing crawl history");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("Listings")]
        public async Task<IActionResult> Listings()
        {
            var premiumArcades = await _context.Arcades
                .Where(a => a.IsPremium)
                .OrderByDescending(a => a.PremiumExpiresAt)
                .ToListAsync();

            return View(premiumArcades);
        }

        [HttpPost("arcades/delete/{id}")]
        public async Task<IActionResult> DeleteArcade(int id)
        {
            var arcade = await _context.Arcades.FindAsync(id);
            if (arcade == null)
            {
                return NotFound();
            }

            _context.Arcades.Remove(arcade);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("games/delete/{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            var game = await _context.VRGames.FindAsync(id);
            if (game == null)
            {
                return NotFound();
            }

            _context.VRGames.Remove(game);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
