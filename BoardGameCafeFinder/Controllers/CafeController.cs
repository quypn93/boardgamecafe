using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoardGameCafeFinder.Controllers;

[Route("cafe")]
public class CafeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CafeController> _logger;
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;

    public CafeController(ApplicationDbContext context, ILogger<CafeController> logger, UserManager<User> userManager, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
        _configuration = configuration;
    }

    private bool RequireReviewApproval => _configuration.GetValue<bool>("ReviewSettings:RequireApproval", true);

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return NotFound();
        }

        var cafe = await _context.Cafes
            .Include(c => c.Photos)
            .Include(c => c.Reviews)
                .ThenInclude(r => r.User)
            .Include(c => c.CafeGames)
                .ThenInclude(cg => cg.Game)
            .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);

        if (cafe == null)
        {
            // Try to find by ID if slug is numeric (backward compatibility)
            if (int.TryParse(slug, out int id))
            {
                cafe = await _context.Cafes
                    .Include(c => c.Photos)
                    .Include(c => c.Reviews)
                        .ThenInclude(r => r.User)
                    .Include(c => c.CafeGames)
                        .ThenInclude(cg => cg.Game)
                    .FirstOrDefaultAsync(c => c.CafeId == id && c.IsActive);

                // Redirect to slug URL if found
                if (cafe != null && !string.IsNullOrEmpty(cafe.Slug))
                {
                    return RedirectToActionPermanent(nameof(Details), new { slug = cafe.Slug });
                }
            }

            if (cafe == null)
            {
                return NotFound();
            }
        }

        // Set SEO metadata for cafe detail page - optimized for CTR
        var ratingText = cafe.AverageRating.HasValue && cafe.TotalReviews > 0
            ? $"★ {cafe.AverageRating:0.0} ({cafe.TotalReviews} reviews). "
            : "";
        var gamesCount = cafe.CafeGames?.Count ?? 0;
        var gamesText = gamesCount > 0 ? $"{gamesCount}+ games available. " : "";

        // Title with rating star for SERP visibility
        var titleSuffix = cafe.AverageRating.HasValue && cafe.AverageRating > 0
            ? $" | {cafe.AverageRating:0.0}★"
            : "";
        ViewData["Title"] = $"{cafe.Name} - Board Game Cafe in {cafe.City}{titleSuffix}";

        // Meta description with social proof and CTA
        var description = $"{cafe.Name} in {cafe.City} - {ratingText}{gamesText}Hours, directions & menu. Plan your visit today!";
        if (description.Length > 160)
        {
            description = description.Substring(0, 157) + "...";
        }

        ViewData["MetaDescription"] = description;
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}/cafe/{cafe.Slug}";

        // Pass review approval settings to view
        ViewBag.RequireReviewApproval = RequireReviewApproval;

        // Get current user ID to check if they can see their own pending reviews
        var currentUserId = User.Identity?.IsAuthenticated == true
            ? (await _userManager.GetUserAsync(User))?.Id
            : (int?)null;
        ViewBag.CurrentUserId = currentUserId;

        return View(cafe);
    }

    /// <summary>
    /// Submit a review for a cafe (requires login)
    /// </summary>
    [HttpPost("{slug}/review")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(string slug, int rating, string? title, string? content, DateTime? visitDate)
    {
        // Find the cafe
        var cafe = await _context.Cafes.FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive);
        if (cafe == null)
        {
            return NotFound();
        }

        // Get current user
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = $"/cafe/{slug}" });
        }

        // Check if user already reviewed this cafe
        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.CafeId == cafe.CafeId && r.UserId == user.Id);

        if (existingReview != null)
        {
            TempData["Error"] = "You have already reviewed this cafe. You can edit your existing review.";
            return RedirectToAction("Details", new { slug });
        }

        // Validate rating
        if (rating < 1 || rating > 5)
        {
            TempData["Error"] = "Rating must be between 1 and 5.";
            return RedirectToAction("Details", new { slug });
        }

        // Check if approval is required
        var requireApproval = RequireReviewApproval;

        // Create review
        var review = new Review
        {
            CafeId = cafe.CafeId,
            UserId = user.Id,
            Rating = rating,
            Title = title?.Trim(),
            Content = content?.Trim(),
            VisitDate = visitDate,
            IsApproved = !requireApproval, // Auto-approve if approval not required
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);

        // Only update cafe rating if review is auto-approved
        if (!requireApproval)
        {
            var allRatings = await _context.Reviews
                .Where(r => r.CafeId == cafe.CafeId && r.IsApproved)
                .Select(r => r.Rating)
                .ToListAsync();

            allRatings.Add(rating);
            cafe.AverageRating = (decimal)allRatings.Average();
            cafe.TotalReviews = allRatings.Count;
        }

        // Update user stats
        user.TotalReviews++;

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} submitted review for cafe {CafeName} (approval required: {RequireApproval})", user.Email, cafe.Name, requireApproval);

        if (requireApproval)
        {
            TempData["Success"] = "Thank you for your review! It will be visible after admin approval.";
        }
        else
        {
            TempData["Success"] = "Thank you for your review!";
        }

        return RedirectToAction("Details", new { slug });
    }

    /// <summary>
    /// Delete a review (owner only)
    /// </summary>
    [HttpPost("{slug}/review/delete/{reviewId}")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(string slug, int reviewId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var review = await _context.Reviews
            .Include(r => r.Cafe)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId);

        if (review == null)
        {
            return NotFound();
        }

        // Only allow owner or admin to delete
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (review.UserId != user.Id && !isAdmin)
        {
            TempData["Error"] = "You can only delete your own reviews.";
            return RedirectToAction("Details", new { slug });
        }

        var cafe = review.Cafe;

        _context.Reviews.Remove(review);

        // Update cafe average rating (only count approved reviews)
        var remainingRatings = await _context.Reviews
            .Where(r => r.CafeId == cafe.CafeId && r.ReviewId != reviewId && r.IsApproved)
            .Select(r => r.Rating)
            .ToListAsync();

        if (remainingRatings.Count > 0)
        {
            cafe.AverageRating = (decimal)remainingRatings.Average();
            cafe.TotalReviews = remainingRatings.Count;
        }
        else
        {
            cafe.AverageRating = null;
            cafe.TotalReviews = 0;
        }

        // Update user stats
        if (review.UserId == user.Id)
        {
            user.TotalReviews = Math.Max(0, user.TotalReviews - 1);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Review deleted successfully.";
        return RedirectToAction("Details", new { slug });
    }
}
