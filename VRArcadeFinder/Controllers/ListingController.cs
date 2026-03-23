using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using VRArcadeFinder.Services;

namespace VRArcadeFinder.Controllers
{
    [Route("listing")]
    public class ListingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<ListingController> _logger;

        // Premium plan pricing
        private static readonly Dictionary<string, decimal> PlanPrices = new()
        {
            { "Basic", 29.99m },
            { "Premium", 59.99m },
            { "Featured", 99.99m }
        };

        // Duration discounts
        private static readonly Dictionary<int, decimal> DurationDiscounts = new()
        {
            { 1, 0m },
            { 3, 5m },
            { 6, 10m },
            { 12, 20m }
        };

        public ListingController(
            ApplicationDbContext context,
            IPaymentService paymentService,
            ILogger<ListingController> logger)
        {
            _context = context;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet("pricing")]
        public IActionResult Pricing()
        {
            ViewBag.PlanPrices = PlanPrices;
            ViewBag.DurationDiscounts = DurationDiscounts;
            return View();
        }

        [HttpGet("claim/{arcadeId}")]
        public async Task<IActionResult> Claim(int arcadeId, string? plan = "Premium", int? months = 1)
        {
            var arcade = await _context.Arcades.FindAsync(arcadeId);
            if (arcade == null)
            {
                return NotFound();
            }

            ViewBag.Arcade = arcade;
            ViewBag.PlanPrices = PlanPrices;
            ViewBag.DurationDiscounts = DurationDiscounts;
            ViewBag.SelectedPlan = plan ?? "Premium";
            ViewBag.SelectedMonths = months ?? 1;

            return View();
        }

        [HttpPost("claim/{arcadeId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitClaim(int arcadeId, [FromForm] ClaimRequest claimRequest)
        {
            var arcade = await _context.Arcades.FindAsync(arcadeId);
            if (arcade == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Arcade = arcade;
                ViewBag.PlanPrices = PlanPrices;
                ViewBag.DurationDiscounts = DurationDiscounts;
                return View("Claim", claimRequest);
            }

            // Calculate pricing
            var monthlyRate = PlanPrices.GetValueOrDefault(claimRequest.PlanType, 59.99m);
            var discountPercent = DurationDiscounts.GetValueOrDefault(claimRequest.DurationMonths, 0m);
            var subtotal = monthlyRate * claimRequest.DurationMonths;
            var discountAmount = subtotal * (discountPercent / 100m);
            var totalAmount = subtotal - discountAmount;

            claimRequest.ArcadeId = arcadeId;
            claimRequest.MonthlyRate = monthlyRate;
            claimRequest.DiscountPercent = discountPercent;
            claimRequest.TotalAmount = totalAmount;
            claimRequest.CreatedAt = DateTime.UtcNow;

            _context.ClaimRequests.Add(claimRequest);
            await _context.SaveChangesAsync();

            // Create payment session
            var successUrl = Url.Action("CheckoutSuccess", "Listing", new { claimId = claimRequest.ClaimRequestId }, Request.Scheme);
            var cancelUrl = Url.Action("CheckoutCancel", "Listing", new { claimId = claimRequest.ClaimRequestId }, Request.Scheme);

            var result = await _paymentService.CreateCheckoutSessionAsync(claimRequest, successUrl!, cancelUrl!);

            if (result.Success && !string.IsNullOrEmpty(result.CheckoutUrl))
            {
                claimRequest.StripeSessionId = result.SessionId;
                claimRequest.PaymentStatus = "processing";
                await _context.SaveChangesAsync();

                return Redirect(result.CheckoutUrl);
            }

            TempData["Error"] = result.ErrorMessage ?? "Failed to create payment session";
            return RedirectToAction("Claim", new { arcadeId });
        }

        [HttpGet("checkout/success")]
        public async Task<IActionResult> CheckoutSuccess(int claimId)
        {
            var claim = await _context.ClaimRequests
                .Include(c => c.Arcade)
                .FirstOrDefaultAsync(c => c.ClaimRequestId == claimId);

            if (claim == null)
            {
                return NotFound();
            }

            // Verify payment was successful
            if (!string.IsNullOrEmpty(claim.StripeSessionId))
            {
                var session = await _paymentService.GetSessionAsync(claim.StripeSessionId);
                if (session?.Status == "completed")
                {
                    claim.PaymentStatus = "completed";
                    claim.PaidAt = DateTime.UtcNow;

                    // Create premium listing
                    var listing = new PremiumListing
                    {
                        ArcadeId = claim.ArcadeId,
                        PlanType = claim.PlanType,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddMonths(claim.DurationMonths),
                        MonthlyFee = claim.MonthlyRate,
                        IsActive = true,
                        FeaturedPlacement = claim.PlanType == "Featured",
                        PhotoGallery = claim.PlanType == "Premium" || claim.PlanType == "Featured",
                        EventListings = claim.PlanType == "Premium" || claim.PlanType == "Featured",
                        GameInventoryManager = claim.PlanType == "Featured",
                        AnalyticsDashboard = claim.PlanType == "Featured",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.PremiumListings.Add(listing);

                    // Update arcade
                    if (claim.Arcade != null)
                    {
                        claim.Arcade.IsPremium = true;
                    }

                    await _context.SaveChangesAsync();

                    claim.PremiumListingId = listing.ListingId;
                    await _context.SaveChangesAsync();
                }
            }

            return View(claim);
        }

        [HttpGet("checkout/cancel")]
        public async Task<IActionResult> CheckoutCancel(int claimId)
        {
            var claim = await _context.ClaimRequests
                .Include(c => c.Arcade)
                .FirstOrDefaultAsync(c => c.ClaimRequestId == claimId);

            if (claim == null)
            {
                return NotFound();
            }

            claim.PaymentStatus = "cancelled";
            await _context.SaveChangesAsync();

            return View(claim);
        }

        [HttpGet("submit")]
        public IActionResult SubmitArcade()
        {
            return View();
        }

        [HttpPost("submit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitArcade([FromForm] Arcade arcade)
        {
            if (!ModelState.IsValid)
            {
                return View(arcade);
            }

            arcade.IsActive = true;
            arcade.IsVerified = false;
            arcade.CreatedAt = DateTime.UtcNow;
            arcade.UpdatedAt = DateTime.UtcNow;

            // Generate slug
            arcade.Slug = GenerateSlug(arcade.Name);

            _context.Arcades.Add(arcade);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your VR arcade has been submitted and will be reviewed shortly.";
            return RedirectToAction("SubmitSuccess");
        }

        [HttpGet("submit/success")]
        public IActionResult SubmitSuccess()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin")]
        public async Task<IActionResult> Admin(string? status = null, int page = 1, int pageSize = 20)
        {
            var query = _context.PremiumListings
                .Include(l => l.Arcade)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "active")
                {
                    query = query.Where(l => l.IsActive && l.EndDate > DateTime.UtcNow);
                }
                else if (status == "expired")
                {
                    query = query.Where(l => l.EndDate <= DateTime.UtcNow);
                }
            }

            var totalCount = await query.CountAsync();
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Status = status;

            var listings = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return View(listings);
        }

        private string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (slug.Length > 100)
            {
                slug = slug.Substring(0, 100).TrimEnd('-');
            }

            slug += "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            return slug;
        }
    }
}
