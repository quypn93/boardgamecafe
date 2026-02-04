using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using BoardGameCafeFinder.Services;
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IBggSyncService _bggSyncService;
        private readonly ApplicationDbContext _context;
        private readonly IBlogService _blogService;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly ICafeWebsiteCrawlerService _cafeWebsiteCrawlerService;
        private readonly IAutoCrawlService _autoCrawlService;

        public AdminController(
            IBggSyncService bggSyncService,
            ApplicationDbContext context,
            IBlogService blogService,
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            ICafeWebsiteCrawlerService cafeWebsiteCrawlerService,
            IAutoCrawlService autoCrawlService)
        {
            _bggSyncService = bggSyncService;
            _context = context;
            _blogService = blogService;
            _userManager = userManager;
            _roleManager = roleManager;
            _cafeWebsiteCrawlerService = cafeWebsiteCrawlerService;
            _autoCrawlService = autoCrawlService;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Get statistics
            var totalCafes = await _context.Cafes.CountAsync();
            var totalGames = await _context.BoardGames.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalReviews = await _context.Reviews.CountAsync();
            var pendingReviews = await _context.Reviews.CountAsync(r => r.UserId != null && !r.IsApproved);

            var recentCafes = await _context.Cafes
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            var topCities = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.City))
                .GroupBy(c => c.City)
                .Select(g => new { City = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalCafes = totalCafes;
            ViewBag.TotalGames = totalGames;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalReviews = totalReviews;
            ViewBag.PendingReviewCount = pendingReviews;
            ViewBag.RecentCafes = recentCafes;
            ViewBag.TopCities = topCities;

            return View();
        }

        public async Task<IActionResult> Index(string bggFilter = "all")
        {
            var query = _context.Cafes
                .Include(c => c.CafeGames)
                .OrderBy(c => c.Name)
                .AsQueryable();

            ViewBag.BggFilter = bggFilter;

            switch (bggFilter.ToLower())
            {
                case "with":
                    query = query.Where(c => !string.IsNullOrEmpty(c.BggUsername));
                    break;
                case "without":
                    query = query.Where(c => string.IsNullOrEmpty(c.BggUsername));
                    break;
                // "all" - no filter
            }

            var cafes = await query.ToListAsync();

            // Get counts for filter badges
            var allCafes = await _context.Cafes.ToListAsync();
            ViewBag.AllCount = allCafes.Count;
            ViewBag.WithBggCount = allCafes.Count(c => !string.IsNullOrEmpty(c.BggUsername));
            ViewBag.WithoutBggCount = allCafes.Count(c => string.IsNullOrEmpty(c.BggUsername));

            var cities = allCafes.Select(c => c.City).Distinct().OrderBy(c => c).ToList();
            ViewBag.Cities = cities;

            return View(cafes);
        }

        [HttpPost]
        public async Task<IActionResult> SyncCafe(int id)
        {
            var result = await _bggSyncService.SyncCafeGamesAsync(id);
            if (result.Success)
            {
                TempData["SuccessMessage"] = $"Successfully synced {result.GamesProcessed} games for cafe.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Sync failed: {result.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SyncCity(string city)
        {
            var results = await _bggSyncService.SyncCafesInCityAsync(city);
            int totalGames = results.Sum(r => r.Result.GamesProcessed);
            TempData["SuccessMessage"] = $"Successfully synced {totalGames} games across {results.Count} cafes in {city}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SyncAll()
        {
            var results = await _bggSyncService.SyncAllCafesAsync();
            int totalGames = results.Sum(r => r.Result.GamesProcessed);
            TempData["SuccessMessage"] = $"Successfully synced {totalGames} games across {results.Count} cafes.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBggUsername(int id, string bggUsername)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null) return NotFound();

            cafe.BggUsername = bggUsername?.Trim();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Updated BGG Username for {cafe.Name}.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// View games for a specific cafe
        /// </summary>
        public async Task<IActionResult> CafeGames(int id)
        {
            var cafe = await _context.Cafes
                .Include(c => c.CafeGames)
                    .ThenInclude(cg => cg.Game)
                .FirstOrDefaultAsync(c => c.CafeId == id);

            if (cafe == null)
                return NotFound();

            return View(cafe);
        }

        /// <summary>
        /// Delete a game from cafe. If no other cafes use it, delete the board game too.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteCafeGame(int cafeId, int gameId)
        {
            var cafeGame = await _context.CafeGames
                .FirstOrDefaultAsync(cg => cg.CafeId == cafeId && cg.GameId == gameId);

            if (cafeGame == null)
            {
                TempData["ErrorMessage"] = "Game not found in this cafe.";
                return RedirectToAction(nameof(CafeGames), new { id = cafeId });
            }

            // Remove the cafe-game link
            _context.CafeGames.Remove(cafeGame);
            await _context.SaveChangesAsync();

            // Check if any other cafes use this board game
            var otherCafesUsingGame = await _context.CafeGames
                .AnyAsync(cg => cg.GameId == gameId);

            if (!otherCafesUsingGame)
            {
                // No other cafes use this game, delete the board game
                var boardGame = await _context.BoardGames.FindAsync(gameId);
                if (boardGame != null)
                {
                    _context.BoardGames.Remove(boardGame);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Removed game from cafe and deleted board game (no longer used).";
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Removed game from cafe.";
            }

            return RedirectToAction(nameof(CafeGames), new { id = cafeId });
        }

        /// <summary>
        /// Delete selected games from a cafe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteSelectedCafeGames(int cafeId, int[] gameIds)
        {
            if (gameIds == null || gameIds.Length == 0)
            {
                TempData["ErrorMessage"] = "No games selected.";
                return RedirectToAction(nameof(CafeGames), new { id = cafeId });
            }

            var cafeGames = await _context.CafeGames
                .Where(cg => cg.CafeId == cafeId && gameIds.Contains(cg.GameId))
                .ToListAsync();

            if (!cafeGames.Any())
            {
                TempData["ErrorMessage"] = "Selected games not found in this cafe.";
                return RedirectToAction(nameof(CafeGames), new { id = cafeId });
            }

            var deletedGameIds = cafeGames.Select(cg => cg.GameId).ToList();

            // Remove the cafe-game links
            _context.CafeGames.RemoveRange(cafeGames);
            await _context.SaveChangesAsync();

            // Check which board games are no longer used by any cafe
            int deletedBoardGames = 0;
            foreach (var gameId in deletedGameIds)
            {
                var isUsedByOtherCafe = await _context.CafeGames.AnyAsync(cg => cg.GameId == gameId);
                if (!isUsedByOtherCafe)
                {
                    var boardGame = await _context.BoardGames.FindAsync(gameId);
                    if (boardGame != null)
                    {
                        _context.BoardGames.Remove(boardGame);
                        deletedBoardGames++;
                    }
                }
            }

            if (deletedBoardGames > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Removed {cafeGames.Count} games from cafe. Deleted {deletedBoardGames} board games no longer used.";
            return RedirectToAction(nameof(CafeGames), new { id = cafeId });
        }

        /// <summary>
        /// Delete all games from a cafe
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteAllCafeGames(int cafeId)
        {
            var cafeGames = await _context.CafeGames
                .Where(cg => cg.CafeId == cafeId)
                .ToListAsync();

            if (!cafeGames.Any())
            {
                TempData["ErrorMessage"] = "No games found for this cafe.";
                return RedirectToAction(nameof(CafeGames), new { id = cafeId });
            }

            var gameIds = cafeGames.Select(cg => cg.GameId).ToList();

            // Remove all cafe-game links
            _context.CafeGames.RemoveRange(cafeGames);
            await _context.SaveChangesAsync();

            // Check which board games are no longer used by any cafe
            int deletedGames = 0;
            foreach (var gameId in gameIds)
            {
                var isUsedByOtherCafe = await _context.CafeGames.AnyAsync(cg => cg.GameId == gameId);
                if (!isUsedByOtherCafe)
                {
                    var boardGame = await _context.BoardGames.FindAsync(gameId);
                    if (boardGame != null)
                    {
                        _context.BoardGames.Remove(boardGame);
                        deletedGames++;
                    }
                }
            }

            if (deletedGames > 0)
            {
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Removed {cafeGames.Count} games from cafe. Deleted {deletedGames} board games no longer used.";
            return RedirectToAction(nameof(CafeGames), new { id = cafeId });
        }

        #region Review Management

        /// <summary>
        /// List all reviews with filter by approval status
        /// </summary>
        public async Task<IActionResult> Reviews(string filter = "pending")
        {
            var query = _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Cafe)
                .OrderByDescending(r => r.CreatedAt)
                .AsQueryable();

            ViewBag.Filter = filter;

            switch (filter.ToLower())
            {
                case "pending":
                    // Only show user-submitted reviews (UserId != null) that are pending approval
                    // Crawled reviews (UserId = null) don't need approval
                    query = query.Where(r => r.UserId != null && !r.IsApproved);
                    break;
                case "approved":
                    query = query.Where(r => r.IsApproved);
                    break;
                // "all" - no filter
            }

            var reviews = await query.ToListAsync();

            // Get counts for filter badges (only count user-submitted reviews for pending)
            ViewBag.PendingCount = await _context.Reviews.CountAsync(r => r.UserId != null && !r.IsApproved);
            ViewBag.ApprovedCount = await _context.Reviews.CountAsync(r => r.IsApproved);
            ViewBag.TotalCount = await _context.Reviews.CountAsync();

            return View(reviews);
        }

        /// <summary>
        /// Approve a review
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApproveReview(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.Cafe)
                .FirstOrDefaultAsync(r => r.ReviewId == id);

            if (review == null)
            {
                TempData["ErrorMessage"] = "Review not found.";
                return RedirectToAction(nameof(Reviews));
            }

            review.IsApproved = true;
            review.ApprovedAt = DateTime.UtcNow;

            // Get admin user ID from claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int adminUserId))
            {
                review.ApprovedByUserId = adminUserId;
            }

            // Update cafe rating
            var cafe = review.Cafe;
            var allApprovedRatings = await _context.Reviews
                .Where(r => r.CafeId == cafe.CafeId && (r.IsApproved || r.ReviewId == id))
                .Select(r => r.Rating)
                .ToListAsync();

            if (allApprovedRatings.Any())
            {
                cafe.AverageRating = (decimal)allApprovedRatings.Average();
                cafe.TotalReviews = allApprovedRatings.Count;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review approved successfully.";
            return RedirectToAction(nameof(Reviews));
        }

        /// <summary>
        /// Reject/delete a review
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RejectReview(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.Cafe)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ReviewId == id);

            if (review == null)
            {
                TempData["ErrorMessage"] = "Review not found.";
                return RedirectToAction(nameof(Reviews));
            }

            var cafe = review.Cafe;
            var wasApproved = review.IsApproved;

            // Update user's TotalReviews if review belongs to a user
            if (review.User != null)
            {
                review.User.TotalReviews = Math.Max(0, review.User.TotalReviews - 1);
            }

            _context.Reviews.Remove(review);

            // Update cafe rating if the deleted review was approved
            if (wasApproved)
            {
                var remainingRatings = await _context.Reviews
                    .Where(r => r.CafeId == cafe.CafeId && r.ReviewId != id && r.IsApproved)
                    .Select(r => r.Rating)
                    .ToListAsync();

                if (remainingRatings.Any())
                {
                    cafe.AverageRating = (decimal)remainingRatings.Average();
                    cafe.TotalReviews = remainingRatings.Count;
                }
                else
                {
                    cafe.AverageRating = null;
                    cafe.TotalReviews = 0;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review rejected and deleted.";
            return RedirectToAction(nameof(Reviews));
        }

        /// <summary>
        /// Bulk approve selected reviews
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BulkApproveReviews(int[] reviewIds)
        {
            if (reviewIds == null || reviewIds.Length == 0)
            {
                TempData["ErrorMessage"] = "No reviews selected.";
                return RedirectToAction(nameof(Reviews));
            }

            var reviews = await _context.Reviews
                .Include(r => r.Cafe)
                .Where(r => reviewIds.Contains(r.ReviewId) && !r.IsApproved)
                .ToListAsync();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            int? adminUserId = null;
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int adminId))
            {
                adminUserId = adminId;
            }

            var affectedCafeIds = new HashSet<int>();

            foreach (var review in reviews)
            {
                review.IsApproved = true;
                review.ApprovedAt = DateTime.UtcNow;
                review.ApprovedByUserId = adminUserId;
                affectedCafeIds.Add(review.CafeId);
            }

            await _context.SaveChangesAsync();

            // Update ratings for affected cafes
            foreach (var cafeId in affectedCafeIds)
            {
                var cafe = await _context.Cafes.FindAsync(cafeId);
                if (cafe != null)
                {
                    var ratings = await _context.Reviews
                        .Where(r => r.CafeId == cafeId && r.IsApproved)
                        .Select(r => r.Rating)
                        .ToListAsync();

                    if (ratings.Any())
                    {
                        cafe.AverageRating = (decimal)ratings.Average();
                        cafe.TotalReviews = ratings.Count;
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Approved {reviews.Count} reviews.";
            return RedirectToAction(nameof(Reviews));
        }

        #endregion

        #region Blog Management

        /// <summary>
        /// List all blog posts
        /// </summary>
        public async Task<IActionResult> BlogPosts()
        {
            var posts = await _blogService.GetAllPostsAsync(includeUnpublished: true);

            // Get cities with cafes for the generate form
            var cities = await _context.Cafes
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.City))
                .GroupBy(c => c.City)
                .Select(g => new { City = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(50)
                .ToListAsync();

            ViewBag.Cities = cities;

            return View(posts);
        }

        /// <summary>
        /// Create new blog post form
        /// </summary>
        public IActionResult CreateBlogPost()
        {
            return View(new Models.Domain.BlogPost());
        }

        /// <summary>
        /// Create new blog post
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBlogPost(Models.Domain.BlogPost post)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            await _blogService.CreatePostAsync(post);
            TempData["SuccessMessage"] = "Blog post created successfully.";
            return RedirectToAction(nameof(BlogPosts));
        }

        /// <summary>
        /// Edit blog post form
        /// </summary>
        public async Task<IActionResult> EditBlogPost(int id)
        {
            var post = await _blogService.GetPostByIdAsync(id);
            if (post == null)
            {
                return NotFound();
            }
            return View(post);
        }

        /// <summary>
        /// Update blog post
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBlogPost(Models.Domain.BlogPost post)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            await _blogService.UpdatePostAsync(post);
            TempData["SuccessMessage"] = "Blog post updated successfully.";
            return RedirectToAction(nameof(BlogPosts));
        }

        /// <summary>
        /// Delete blog post
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteBlogPost(int id)
        {
            var result = await _blogService.DeletePostAsync(id);
            if (result)
            {
                TempData["SuccessMessage"] = "Blog post deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Blog post not found.";
            }
            return RedirectToAction(nameof(BlogPosts));
        }

        /// <summary>
        /// Toggle publish status
        /// </summary>
        [HttpPost]
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

            TempData["SuccessMessage"] = post.IsPublished
                ? "Blog post published."
                : "Blog post unpublished.";

            return RedirectToAction(nameof(BlogPosts));
        }

        /// <summary>
        /// Generate top games post for selected cities
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateTopGamesPosts([FromBody] GeneratePostsRequest request)
        {
            if (request.Cities == null || !request.Cities.Any())
            {
                return Json(new { success = false, message = "No cities selected." });
            }

            try
            {
                var posts = await _blogService.GenerateTopGamesPostsForCitiesAsync(request.Cities);
                return Json(new {
                    success = true,
                    message = $"Generated {posts.Count} posts successfully.",
                    count = posts.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Generate city guide post
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateCityGuidePost([FromBody] GenerateCityGuideRequest request)
        {
            if (string.IsNullOrEmpty(request.City))
            {
                return Json(new { success = false, message = "City is required." });
            }

            try
            {
                var post = await _blogService.GenerateCityGuidePostAsync(request.City);
                return Json(new {
                    success = true,
                    message = $"Generated city guide for {request.City}.",
                    postId = post.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Affiliate Management

        /// <summary>
        /// List all games with affiliate management
        /// </summary>
        public async Task<IActionResult> ManageAffiliates(string filter = "all", string search = "")
        {
            var query = _context.BoardGames
                .Include(g => g.CafeGames)
                .OrderBy(g => g.Name)
                .AsQueryable();

            ViewBag.Filter = filter;
            ViewBag.Search = search;

            // Apply filter
            switch (filter.ToLower())
            {
                case "with":
                    query = query.Where(g => !string.IsNullOrEmpty(g.AmazonAffiliateUrl));
                    break;
                case "without":
                    query = query.Where(g => string.IsNullOrEmpty(g.AmazonAffiliateUrl));
                    break;
            }

            // Apply search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(g => g.Name.ToLower().Contains(searchLower));
            }

            var games = await query.Take(100).ToListAsync();

            // Get counts for badges
            ViewBag.AllCount = await _context.BoardGames.CountAsync();
            ViewBag.WithAffiliateCount = await _context.BoardGames.CountAsync(g => !string.IsNullOrEmpty(g.AmazonAffiliateUrl));
            ViewBag.WithoutAffiliateCount = await _context.BoardGames.CountAsync(g => string.IsNullOrEmpty(g.AmazonAffiliateUrl));

            // Get click statistics
            var clickStats = await _context.AffiliateClicks
                .GroupBy(c => c.GameId)
                .Select(g => new { GameId = g.Key, ClickCount = g.Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.ClickCount);
            ViewBag.ClickStats = clickStats;

            return View(games);
        }

        /// <summary>
        /// Edit affiliate URL for a game
        /// </summary>
        public async Task<IActionResult> EditGameAffiliate(int id)
        {
            var game = await _context.BoardGames
                .Include(g => g.CafeGames)
                    .ThenInclude(cg => cg.Cafe)
                .FirstOrDefaultAsync(g => g.GameId == id);

            if (game == null)
                return NotFound();

            // Get click count for this game
            ViewBag.ClickCount = await _context.AffiliateClicks.CountAsync(c => c.GameId == id);

            return View(game);
        }

        /// <summary>
        /// Update affiliate URL for a game
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGameAffiliateUrl(int gameId, string? amazonAffiliateUrl, string? miniatureMarketAffiliateUrl)
        {
            var game = await _context.BoardGames.FindAsync(gameId);
            if (game == null)
            {
                TempData["ErrorMessage"] = "Game not found.";
                return RedirectToAction(nameof(ManageAffiliates));
            }

            // Validate Amazon URL if provided
            if (!string.IsNullOrWhiteSpace(amazonAffiliateUrl))
            {
                amazonAffiliateUrl = amazonAffiliateUrl.Trim();
                if (!Uri.TryCreate(amazonAffiliateUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    TempData["ErrorMessage"] = "Invalid Amazon URL format.";
                    return RedirectToAction(nameof(EditGameAffiliate), new { id = gameId });
                }
            }

            // Validate Miniature Market URL if provided
            if (!string.IsNullOrWhiteSpace(miniatureMarketAffiliateUrl))
            {
                miniatureMarketAffiliateUrl = miniatureMarketAffiliateUrl.Trim();
                if (!Uri.TryCreate(miniatureMarketAffiliateUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    TempData["ErrorMessage"] = "Invalid Miniature Market URL format.";
                    return RedirectToAction(nameof(EditGameAffiliate), new { id = gameId });
                }
            }

            game.AmazonAffiliateUrl = string.IsNullOrWhiteSpace(amazonAffiliateUrl) ? null : amazonAffiliateUrl;
            game.MiniatureMarketAffiliateUrl = string.IsNullOrWhiteSpace(miniatureMarketAffiliateUrl) ? null : miniatureMarketAffiliateUrl;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Affiliate URLs updated for '{game.Name}'.";
            return RedirectToAction(nameof(ManageAffiliates));
        }

        /// <summary>
        /// Bulk update affiliate URLs
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdateAffiliateUrls(Dictionary<int, string> affiliateUrls)
        {
            if (affiliateUrls == null || !affiliateUrls.Any())
            {
                TempData["ErrorMessage"] = "No URLs provided.";
                return RedirectToAction(nameof(ManageAffiliates));
            }

            var gameIds = affiliateUrls.Keys.ToList();
            var games = await _context.BoardGames
                .Where(g => gameIds.Contains(g.GameId))
                .ToListAsync();

            int updated = 0;
            foreach (var game in games)
            {
                if (affiliateUrls.TryGetValue(game.GameId, out var url))
                {
                    var newUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
                    if (game.AmazonAffiliateUrl != newUrl)
                    {
                        game.AmazonAffiliateUrl = newUrl;
                        updated++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Updated {updated} affiliate URLs.";
            return RedirectToAction(nameof(ManageAffiliates));
        }

        /// <summary>
        /// Import affiliate URLs from CSV
        /// </summary>
        public IActionResult ImportAffiliateUrls()
        {
            return View();
        }

        /// <summary>
        /// Process CSV import
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportAffiliateUrls(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a CSV file.";
                return View();
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "File must be a CSV.";
                return View();
            }

            var results = new List<string>();
            int updated = 0, skipped = 0, errors = 0;

            try
            {
                using var reader = new StreamReader(csvFile.OpenReadStream());
                string? line;
                int lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;

                    // Skip header row
                    if (lineNumber == 1 && (line.ToLower().Contains("gameid") || line.ToLower().Contains("bggid")))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 2)
                    {
                        errors++;
                        continue;
                    }

                    var identifier = parts[0].Trim().Trim('"');
                    var url = parts[1].Trim().Trim('"');

                    Models.Domain.BoardGame? game = null;

                    // Try to find game by GameId or BGGId
                    if (int.TryParse(identifier, out int id))
                    {
                        game = await _context.BoardGames.FirstOrDefaultAsync(g => g.GameId == id || g.BGGId == id);
                    }

                    if (game == null)
                    {
                        // Try to find by name
                        game = await _context.BoardGames.FirstOrDefaultAsync(g => g.Name.ToLower() == identifier.ToLower());
                    }

                    if (game == null)
                    {
                        skipped++;
                        results.Add($"Line {lineNumber}: Game '{identifier}' not found.");
                        continue;
                    }

                    // Get Miniature Market URL if provided (3rd column)
                    var mmUrl = parts.Length > 2 ? parts[2].Trim().Trim('"') : null;

                    // Validate Amazon URL
                    if (!string.IsNullOrWhiteSpace(url) &&
                        !Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        errors++;
                        results.Add($"Line {lineNumber}: Invalid Amazon URL for '{game.Name}'.");
                        continue;
                    }

                    // Validate Miniature Market URL
                    if (!string.IsNullOrWhiteSpace(mmUrl) &&
                        !Uri.TryCreate(mmUrl, UriKind.Absolute, out _))
                    {
                        errors++;
                        results.Add($"Line {lineNumber}: Invalid Miniature Market URL for '{game.Name}'.");
                        continue;
                    }

                    game.AmazonAffiliateUrl = string.IsNullOrWhiteSpace(url) ? null : url;
                    if (!string.IsNullOrWhiteSpace(mmUrl))
                    {
                        game.MiniatureMarketAffiliateUrl = mmUrl;
                    }
                    updated++;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Import complete: {updated} updated, {skipped} skipped, {errors} errors.";
                ViewBag.ImportResults = results;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Import failed: {ex.Message}";
            }

            return View();
        }

        /// <summary>
        /// Export games without affiliate URLs as CSV
        /// </summary>
        public async Task<IActionResult> ExportGamesWithoutAffiliate()
        {
            // Export games missing at least one affiliate URL
            var games = await _context.BoardGames
                .Where(g => string.IsNullOrEmpty(g.AmazonAffiliateUrl) || string.IsNullOrEmpty(g.MiniatureMarketAffiliateUrl))
                .OrderBy(g => g.Name)
                .Select(g => new { g.GameId, g.BGGId, g.Name, g.AmazonAffiliateUrl, g.MiniatureMarketAffiliateUrl })
                .ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("GameId,BGGId,Name,AmazonAffiliateUrl,MiniatureMarketAffiliateUrl");

            foreach (var game in games)
            {
                var name = game.Name.Replace("\"", "\"\"");
                var amazonUrl = (game.AmazonAffiliateUrl ?? "").Replace("\"", "\"\"");
                var mmUrl = (game.MiniatureMarketAffiliateUrl ?? "").Replace("\"", "\"\"");
                csv.AppendLine($"{game.GameId},{game.BGGId ?? 0},\"{name}\",\"{amazonUrl}\",\"{mmUrl}\"");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"games_without_affiliate_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        /// <summary>
        /// Affiliate click statistics
        /// </summary>
        public async Task<IActionResult> AffiliateStats()
        {
            // Total clicks
            var totalClicks = await _context.AffiliateClicks.CountAsync();

            // Clicks today
            var today = DateTime.UtcNow.Date;
            var clicksToday = await _context.AffiliateClicks.CountAsync(c => c.ClickedAt >= today);

            // Clicks this month
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var clicksThisMonth = await _context.AffiliateClicks.CountAsync(c => c.ClickedAt >= monthStart);

            // Top games by clicks
            var topGames = await _context.AffiliateClicks
                .GroupBy(c => c.GameId)
                .Select(g => new { GameId = g.Key, Clicks = g.Count() })
                .OrderByDescending(x => x.Clicks)
                .Take(10)
                .ToListAsync();

            var topGameIds = topGames.Select(x => x.GameId).ToList();
            var gameNames = await _context.BoardGames
                .Where(g => topGameIds.Contains(g.GameId))
                .ToDictionaryAsync(g => g.GameId, g => g.Name);

            // Clicks by day (last 30 days)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30).Date;
            var clicksByDay = await _context.AffiliateClicks
                .Where(c => c.ClickedAt >= thirtyDaysAgo)
                .GroupBy(c => c.ClickedAt.Date)
                .Select(g => new { Date = g.Key, Clicks = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            ViewBag.TotalClicks = totalClicks;
            ViewBag.ClicksToday = clicksToday;
            ViewBag.ClicksThisMonth = clicksThisMonth;
            ViewBag.TopGames = topGames.Select(x => new {
                GameName = gameNames.GetValueOrDefault(x.GameId, "Unknown"),
                x.Clicks
            }).ToList();
            ViewBag.ClicksByDay = clicksByDay;

            return View();
        }

        #endregion

        #region User Management

        /// <summary>
        /// List all users with filtering and search
        /// </summary>
        public async Task<IActionResult> Users(string filter = "all", string search = "", int page = 1)
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
                case "cafeowner":
                    query = query.Where(u => u.IsCafeOwner);
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
            ViewBag.CafeOwnerCount = await _context.Users.CountAsync(u => u.IsCafeOwner);
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

        /// <summary>
        /// Edit user form
        /// </summary>
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
            ViewBag.Cafes = await _context.Cafes.OrderBy(c => c.Name).ToListAsync();

            return View(user);
        }

        /// <summary>
        /// Update user info
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUser(int id, string firstName, string lastName, string? displayName,
            string? city, string? country, bool isCafeOwner, int? cafeId, string[] roles)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Update user properties
            user.FirstName = firstName;
            user.LastName = lastName;
            user.DisplayName = displayName ?? $"{firstName} {lastName}";
            user.City = city;
            user.Country = country;
            user.IsCafeOwner = isCafeOwner;
            user.CafeId = isCafeOwner ? cafeId : null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(EditUser), new { id });
            }

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Except(roles ?? Array.Empty<string>());
            var rolesToAdd = (roles ?? Array.Empty<string>()).Except(currentRoles);

            await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            await _userManager.AddToRolesAsync(user, rolesToAdd);

            TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        /// <summary>
        /// Toggle user lockout status
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleUserLockout(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Don't allow locking out yourself
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
            {
                TempData["ErrorMessage"] = "You cannot lock out yourself.";
                return RedirectToAction(nameof(Users));
            }

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                // Unlock user
                await _userManager.SetLockoutEndDateAsync(user, null);
                TempData["SuccessMessage"] = $"User '{user.Email}' has been unlocked.";
            }
            else
            {
                // Lock user for 100 years (effectively permanent)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                TempData["SuccessMessage"] = $"User '{user.Email}' has been locked out.";
            }

            return RedirectToAction(nameof(Users));
        }

        /// <summary>
        /// Delete user
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            // Don't allow deleting yourself
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id.ToString())
            {
                TempData["ErrorMessage"] = "You cannot delete yourself.";
                return RedirectToAction(nameof(Users));
            }

            // Check if user is an admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ErrorMessage"] = "Cannot delete admin users. Remove admin role first.";
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
                TempData["SuccessMessage"] = $"User '{email}' and {userReviews.Count} reviews have been deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }

        /// <summary>
        /// Reset user password
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResetUserPassword(int id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["ErrorMessage"] = "Password must be at least 8 characters.";
                return RedirectToAction(nameof(EditUser), new { id });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Password for '{user.Email}' has been reset.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(EditUser), new { id });
        }

        /// <summary>
        /// Confirm user email manually
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ConfirmUserEmail(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Users));
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Email for '{user.Email}' has been confirmed.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(EditUser), new { id });
        }

        #endregion

        #region Description Management

        /// <summary>
        /// List all cafes with description status for management
        /// </summary>
        public async Task<IActionResult> Descriptions(string filter = "pending")
        {
            var query = _context.Cafes
                .OrderBy(c => c.Name)
                .AsQueryable();

            ViewBag.Filter = filter;

            switch (filter.ToLower())
            {
                case "pending":
                    // Cafes with description but not approved
                    query = query.Where(c => !string.IsNullOrEmpty(c.Description) && !c.IsDescriptionApproved);
                    break;
                case "approved":
                    query = query.Where(c => c.IsDescriptionApproved);
                    break;
                case "empty":
                    query = query.Where(c => string.IsNullOrEmpty(c.Description));
                    break;
                // "all" - no filter
            }

            var cafes = await query.ToListAsync();

            // Get counts for filter badges
            ViewBag.PendingCount = await _context.Cafes.CountAsync(c => !string.IsNullOrEmpty(c.Description) && !c.IsDescriptionApproved);
            ViewBag.ApprovedCount = await _context.Cafes.CountAsync(c => c.IsDescriptionApproved);
            ViewBag.EmptyCount = await _context.Cafes.CountAsync(c => string.IsNullOrEmpty(c.Description));
            ViewBag.TotalCount = await _context.Cafes.CountAsync();

            return View(cafes);
        }

        /// <summary>
        /// Approve a cafe's description
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ApproveDescription(int id)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null)
            {
                TempData["ErrorMessage"] = "Cafe not found.";
                return RedirectToAction(nameof(Descriptions));
            }

            if (string.IsNullOrEmpty(cafe.Description))
            {
                TempData["ErrorMessage"] = "Cannot approve empty description.";
                return RedirectToAction(nameof(Descriptions));
            }

            cafe.IsDescriptionApproved = true;
            cafe.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Description approved for '{cafe.Name}'.";
            return RedirectToAction(nameof(Descriptions));
        }

        /// <summary>
        /// Reject/clear a cafe's description
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RejectDescription(int id)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null)
            {
                TempData["ErrorMessage"] = "Cafe not found.";
                return RedirectToAction(nameof(Descriptions));
            }

            cafe.Description = null;
            cafe.IsDescriptionApproved = false;
            cafe.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Description rejected and cleared for '{cafe.Name}'.";
            return RedirectToAction(nameof(Descriptions));
        }

        /// <summary>
        /// Edit cafe description form
        /// </summary>
        public async Task<IActionResult> EditDescription(int id)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null)
                return NotFound();

            return View(cafe);
        }

        /// <summary>
        /// Update cafe description
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDescription(int id, string? description, bool isApproved)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null)
            {
                TempData["ErrorMessage"] = "Cafe not found.";
                return RedirectToAction(nameof(Descriptions));
            }

            cafe.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            cafe.IsDescriptionApproved = isApproved && !string.IsNullOrEmpty(cafe.Description);
            cafe.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Description updated for '{cafe.Name}'.";
            return RedirectToAction(nameof(Descriptions));
        }

        /// <summary>
        /// Crawl description from cafe's website
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrawlDescription(int id)
        {
            var cafe = await _context.Cafes.FindAsync(id);
            if (cafe == null)
            {
                TempData["ErrorMessage"] = "Cafe not found.";
                return RedirectToAction(nameof(Descriptions));
            }

            if (string.IsNullOrEmpty(cafe.Website))
            {
                TempData["ErrorMessage"] = $"No website URL for '{cafe.Name}'.";
                return RedirectToAction(nameof(Descriptions));
            }

            try
            {
                var description = await _cafeWebsiteCrawlerService.CrawlCafeDescriptionAsync(cafe.Website);

                if (!string.IsNullOrEmpty(description))
                {
                    cafe.Description = description;
                    cafe.IsDescriptionApproved = false; // Requires admin approval
                    cafe.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Description crawled for '{cafe.Name}' ({description.Length} chars). Please review and approve.";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Could not extract description from '{cafe.Website}'.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error crawling description: {ex.Message}";
            }

            return RedirectToAction(nameof(Descriptions));
        }

        /// <summary>
        /// Bulk approve selected descriptions
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BulkApproveDescriptions(int[] cafeIds)
        {
            if (cafeIds == null || cafeIds.Length == 0)
            {
                TempData["ErrorMessage"] = "No cafes selected.";
                return RedirectToAction(nameof(Descriptions));
            }

            var cafes = await _context.Cafes
                .Where(c => cafeIds.Contains(c.CafeId) && !string.IsNullOrEmpty(c.Description) && !c.IsDescriptionApproved)
                .ToListAsync();

            foreach (var cafe in cafes)
            {
                cafe.IsDescriptionApproved = true;
                cafe.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Approved descriptions for {cafes.Count} cafes.";
            return RedirectToAction(nameof(Descriptions));
        }

        /// <summary>
        /// Bulk crawl descriptions for cafes with websites but no description
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BulkCrawlDescriptions(int maxCafes = 10)
        {
            var cafes = await _context.Cafes
                .Where(c => string.IsNullOrEmpty(c.Description) && !string.IsNullOrEmpty(c.Website))
                .Take(maxCafes)
                .ToListAsync();

            if (!cafes.Any())
            {
                TempData["ErrorMessage"] = "No cafes with websites and empty descriptions found.";
                return RedirectToAction(nameof(Descriptions));
            }

            int success = 0, failed = 0;

            foreach (var cafe in cafes)
            {
                try
                {
                    var description = await _cafeWebsiteCrawlerService.CrawlCafeDescriptionAsync(cafe.Website!);
                    if (!string.IsNullOrEmpty(description))
                    {
                        cafe.Description = description;
                        cafe.IsDescriptionApproved = false;
                        cafe.UpdatedAt = DateTime.UtcNow;
                        success++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Crawled descriptions: {success} successful, {failed} failed. Please review and approve.";
            return RedirectToAction(nameof(Descriptions));
        }

        #endregion

        #region Auto Crawl Management

        /// <summary>
        /// List all cities with crawl status
        /// </summary>
        public async Task<IActionResult> Cities(string filter = "all", string region = "all", string search = "", int page = 1, int pageSize = 50)
        {
            var query = _context.Cities
                .OrderBy(c => c.CrawlCount)
                .ThenBy(c => c.Name)
                .AsQueryable();

            ViewBag.Filter = filter;
            ViewBag.Region = region;
            ViewBag.Search = search;

            // Apply region filter
            if (region != "all")
            {
                query = query.Where(c => c.Region == region);
            }

            // Apply status filter
            switch (filter.ToLower())
            {
                case "never":
                    query = query.Where(c => c.CrawlCount == 0);
                    break;
                case "failed":
                    query = query.Where(c => c.LastCrawlStatus == "Failed");
                    break;
                case "pending":
                    query = query.Where(c => c.NextCrawlAt != null && c.NextCrawlAt <= DateTime.UtcNow);
                    break;
                case "inactive":
                    query = query.Where(c => !c.IsActive);
                    break;
            }

            // Apply search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(searchLower) ||
                    (c.Country != null && c.Country.ToLower().Contains(searchLower)));
            }

            // Get counts
            ViewBag.TotalCount = await _context.Cities.CountAsync();
            ViewBag.NeverCrawledCount = await _context.Cities.CountAsync(c => c.CrawlCount == 0);
            ViewBag.FailedCount = await _context.Cities.CountAsync(c => c.LastCrawlStatus == "Failed");
            ViewBag.PendingCount = await _context.Cities.CountAsync(c => c.NextCrawlAt != null && c.NextCrawlAt <= DateTime.UtcNow);
            ViewBag.USCount = await _context.Cities.CountAsync(c => c.Region == "US");
            ViewBag.InternationalCount = await _context.Cities.CountAsync(c => c.Region == "International");
            ViewBag.IsAutoCrawlRunning = _autoCrawlService.IsRunning;

            // Pagination
            var totalItems = await query.CountAsync();
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;

            var cities = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(cities);
        }

        /// <summary>
        /// Crawl status dashboard
        /// </summary>
        public async Task<IActionResult> CrawlStatus()
        {
            // Recent crawl history
            var recentHistory = await _context.CrawlHistories
                .Include(h => h.City)
                .OrderByDescending(h => h.StartedAt)
                .Take(50)
                .ToListAsync();

            // Statistics
            ViewBag.TotalCities = await _context.Cities.CountAsync();
            ViewBag.CitiesCrawled = await _context.Cities.CountAsync(c => c.CrawlCount > 0);
            ViewBag.CitiesNeverCrawled = await _context.Cities.CountAsync(c => c.CrawlCount == 0);
            ViewBag.CitiesFailed = await _context.Cities.CountAsync(c => c.LastCrawlStatus == "Failed");
            ViewBag.TotalCrawls = await _context.CrawlHistories.CountAsync();
            ViewBag.SuccessfulCrawls = await _context.CrawlHistories.CountAsync(h => h.Status == "Success");
            ViewBag.FailedCrawls = await _context.CrawlHistories.CountAsync(h => h.Status == "Failed");
            ViewBag.TotalCafesFound = await _context.CrawlHistories.SumAsync(h => h.CafesFound);
            ViewBag.TotalCafesAdded = await _context.CrawlHistories.SumAsync(h => h.CafesAdded);
            ViewBag.IsAutoCrawlRunning = _autoCrawlService.IsRunning;

            // Next cities to crawl
            var nextCities = await _autoCrawlService.GetNextCitiesToCrawlAsync(5);
            ViewBag.NextCities = nextCities;

            return View(recentHistory);
        }

        /// <summary>
        /// Manually trigger crawl for a specific city
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrawlCity(int id)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                return Json(new { success = false, message = "City not found." });
            }

            try
            {
                var result = await _autoCrawlService.CrawlCityAsync(city);

                // Update city stats
                if (result.Success)
                {
                    city.CrawlCount++;
                    city.LastCrawledAt = DateTime.UtcNow;
                    city.LastCrawlStatus = "Success";
                    city.NextCrawlAt = null;
                }
                else
                {
                    city.LastCrawlStatus = "Failed";
                    city.NextCrawlAt = DateTime.UtcNow.AddHours(2);
                }
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = result.Success,
                    message = result.Success
                        ? $"Crawled {city.Name}: Found {result.CafesFound}, Added {result.CafesAdded}, Updated {result.CafesUpdated}"
                        : $"Crawl failed for {city.Name}: {result.ErrorMessage}",
                    cafesFound = result.CafesFound,
                    cafesAdded = result.CafesAdded,
                    cafesUpdated = result.CafesUpdated
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Toggle city active status
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ToggleCityActive(int id)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                TempData["ErrorMessage"] = "City not found.";
                return RedirectToAction(nameof(Cities));
            }

            city.IsActive = !city.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = city.IsActive
                ? $"'{city.Name}' is now active for auto crawl."
                : $"'{city.Name}' is now excluded from auto crawl.";

            return RedirectToAction(nameof(Cities));
        }

        /// <summary>
        /// Update city max results
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateCityMaxResults(int id, int maxResults)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                return Json(new { success = false, message = "City not found." });
            }

            city.MaxResults = Math.Max(5, Math.Min(50, maxResults));
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Max results for '{city.Name}' updated to {city.MaxResults}." });
        }

        /// <summary>
        /// Add a new city
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddCity(string name, string? country, string region, int maxResults = 15)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "City name is required.";
                return RedirectToAction(nameof(Cities));
            }

            // Check if city already exists
            var existing = await _context.Cities
                .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower().Trim() &&
                    (c.Country == null || c.Country.ToLower() == (country ?? "").ToLower().Trim()));

            if (existing != null)
            {
                TempData["ErrorMessage"] = $"City '{name}' already exists.";
                return RedirectToAction(nameof(Cities));
            }

            var city = new City
            {
                Name = name.Trim(),
                Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim(),
                Region = region == "International" ? "International" : "US",
                MaxResults = Math.Max(5, Math.Min(50, maxResults)),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"City '{city.Name}' added successfully.";
            return RedirectToAction(nameof(Cities));
        }

        /// <summary>
        /// Delete a city
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteCity(int id)
        {
            var city = await _context.Cities
                .Include(c => c.CrawlHistories)
                .FirstOrDefaultAsync(c => c.CityId == id);

            if (city == null)
            {
                TempData["ErrorMessage"] = "City not found.";
                return RedirectToAction(nameof(Cities));
            }

            var cityName = city.Name;

            // Delete crawl history first
            _context.CrawlHistories.RemoveRange(city.CrawlHistories);
            _context.Cities.Remove(city);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"City '{cityName}' and its crawl history have been deleted.";
            return RedirectToAction(nameof(Cities));
        }

        /// <summary>
        /// Reset city crawl status (clear NextCrawlAt to allow immediate retry)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResetCityCrawlStatus(int id)
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null)
            {
                TempData["ErrorMessage"] = "City not found.";
                return RedirectToAction(nameof(Cities));
            }

            city.NextCrawlAt = null;
            city.LastCrawlStatus = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Crawl status reset for '{city.Name}'.";
            return RedirectToAction(nameof(Cities));
        }

        /// <summary>
        /// Stop auto crawl
        /// </summary>
        [HttpPost]
        public IActionResult StopAutoCrawl()
        {
            _autoCrawlService.Stop();
            TempData["SuccessMessage"] = "Auto crawl stop requested.";
            return RedirectToAction(nameof(CrawlStatus));
        }

        /// <summary>
        /// Seed cities if not already seeded
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SeedCities()
        {
            await _autoCrawlService.SeedCitiesAsync();
            TempData["SuccessMessage"] = "Cities seeded successfully.";
            return RedirectToAction(nameof(Cities));
        }

        /// <summary>
        /// Bulk crawl selected cities
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> BulkCrawlCities([FromBody] int[] cityIds)
        {
            if (cityIds == null || cityIds.Length == 0)
            {
                return Json(new { success = false, message = "No cities selected." });
            }

            var cities = await _context.Cities
                .Where(c => cityIds.Contains(c.CityId))
                .ToListAsync();

            int success = 0, failed = 0;
            var results = new List<string>();

            foreach (var city in cities)
            {
                try
                {
                    var result = await _autoCrawlService.CrawlCityAsync(city);

                    if (result.Success)
                    {
                        city.CrawlCount++;
                        city.LastCrawledAt = DateTime.UtcNow;
                        city.LastCrawlStatus = "Success";
                        city.NextCrawlAt = null;
                        success++;
                        results.Add($"{city.Name}: Found {result.CafesFound}, Added {result.CafesAdded}");
                    }
                    else
                    {
                        city.LastCrawlStatus = "Failed";
                        city.NextCrawlAt = DateTime.UtcNow.AddHours(2);
                        failed++;
                        results.Add($"{city.Name}: Failed - {result.ErrorMessage}");
                        break; // Stop on failure
                    }

                    await _context.SaveChangesAsync();

                    // Delay between cities
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
                catch (Exception ex)
                {
                    failed++;
                    results.Add($"{city.Name}: Error - {ex.Message}");
                    break;
                }
            }

            return Json(new
            {
                success = failed == 0,
                message = $"Crawled {success} cities successfully, {failed} failed.",
                results
            });
        }

        #endregion

        #region Country Cleanup

        private static readonly HashSet<string> ValidCountries = new(StringComparer.OrdinalIgnoreCase)
        {
            "United States", "USA", "US",
            "United Kingdom", "UK", "Great Britain", "England", "Scotland", "Wales", "Northern Ireland",
            "Canada", "Australia", "Germany", "France", "Italy", "Spain", "Netherlands", "Belgium",
            "Japan", "South Korea", "Korea", "China", "Taiwan", "Hong Kong", "Singapore", "Malaysia",
            "Thailand", "Vietnam", "Indonesia", "Philippines", "India", "Brazil", "Mexico", "Argentina",
            "Chile", "Colombia", "Poland", "Czech Republic", "Austria", "Switzerland", "Sweden",
            "Norway", "Denmark", "Finland", "Ireland", "Portugal", "Greece", "Turkey", "Russia",
            "Ukraine", "New Zealand", "South Africa"
        };

        [HttpGet]
        public async Task<IActionResult> CleanupCountries()
        {
            var allCountries = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.Country))
                .Select(c => c.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var invalidCountries = allCountries
                .Where(c => !ValidCountries.Contains(c))
                .ToList();

            var cafesWithInvalidCountry = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.Country) && invalidCountries.Contains(c.Country))
                .Select(c => new { c.CafeId, c.Name, c.Country, c.Address })
                .ToListAsync();

            ViewBag.InvalidCountries = invalidCountries;
            ViewBag.CafesWithInvalidCountry = cafesWithInvalidCountry;
            ViewBag.ValidCountries = ValidCountries.OrderBy(c => c).ToList();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FixInvalidCountries()
        {
            var cafesWithInvalidCountry = await _context.Cafes
                .Where(c => !string.IsNullOrEmpty(c.Country) && !ValidCountries.Contains(c.Country))
                .ToListAsync();

            int fixed_count = 0;
            foreach (var cafe in cafesWithInvalidCountry)
            {
                var detectedCountry = DetectCountryFromAddress(cafe.Address);
                if (!string.IsNullOrEmpty(detectedCountry))
                {
                    cafe.Country = detectedCountry;
                    fixed_count++;
                }
                else
                {
                    // Default to United States if can't detect
                    cafe.Country = "United States";
                    fixed_count++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Fixed {fixed_count} cafes with invalid country values.";
            return RedirectToAction(nameof(CleanupCountries));
        }

        [HttpPost]
        public async Task<IActionResult> SetCountryForCafes([FromBody] SetCountryRequest request)
        {
            if (string.IsNullOrEmpty(request.Country) || request.CafeIds == null || !request.CafeIds.Any())
            {
                return Json(new { success = false, message = "Invalid request" });
            }

            var cafes = await _context.Cafes
                .Where(c => request.CafeIds.Contains(c.CafeId))
                .ToListAsync();

            foreach (var cafe in cafes)
            {
                cafe.Country = request.Country;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Updated {cafes.Count} cafes to {request.Country}" });
        }

        private string? DetectCountryFromAddress(string? address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            // Japanese address patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(address, @"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]") ||
                System.Text.RegularExpressions.Regex.IsMatch(address, @"Chome|丁目|番地|号|〒\d{3}-\d{4}|都|道|府|県", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "Japan";
            }

            // Korean address patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(address, @"[\uAC00-\uD7AF]") ||
                System.Text.RegularExpressions.Regex.IsMatch(address, @"층|동|호|로|길|구|시|도", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "South Korea";
            }

            // Vietnamese address patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(address, @"Việt Nam|Vietnam|Quận|Phường|Đường|Thành phố|TP\.|Hồ Chí Minh|Hà Nội", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return "Vietnam";
            }

            // US state abbreviation patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(address, @",\s*[A-Z]{2}\s+\d{5}"))
            {
                return "United States";
            }

            return null;
        }

        #endregion
    }

    public class SetCountryRequest
    {
        public string Country { get; set; } = string.Empty;
        public List<int> CafeIds { get; set; } = new();
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
