using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using BoardGameCafeFinder.Services;
using BoardGameCafeFinder.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IBggSyncService _bggSyncService;
        private readonly ApplicationDbContext _context;
        private readonly IBlogService _blogService;

        public AdminController(IBggSyncService bggSyncService, ApplicationDbContext context, IBlogService blogService)
        {
            _bggSyncService = bggSyncService;
            _context = context;
            _blogService = blogService;
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
