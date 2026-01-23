using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Controllers
{
    [Route("[controller]")]
    public class ListingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ListingController> _logger;

        // Plan definitions
        public static readonly Dictionary<string, ListingPlan> Plans = new()
        {
            ["Basic"] = new ListingPlan
            {
                Name = "Basic",
                MonthlyPrice = 29.99m,
                Features = new[] { "Basic listing", "Contact information", "Business hours", "Location on map" },
                FeaturedPlacement = false,
                PhotoGallery = false,
                EventListings = false,
                GameInventoryManager = false,
                AnalyticsDashboard = false
            },
            ["Premium"] = new ListingPlan
            {
                Name = "Premium",
                MonthlyPrice = 59.99m,
                Features = new[] { "Everything in Basic", "Photo gallery (up to 10)", "Event listings", "Priority in search results" },
                FeaturedPlacement = false,
                PhotoGallery = true,
                EventListings = true,
                GameInventoryManager = false,
                AnalyticsDashboard = false
            },
            ["Featured"] = new ListingPlan
            {
                Name = "Featured",
                MonthlyPrice = 99.99m,
                Features = new[] { "Everything in Premium", "Featured placement on homepage", "Game inventory manager", "Analytics dashboard", "Unlimited photos" },
                FeaturedPlacement = true,
                PhotoGallery = true,
                EventListings = true,
                GameInventoryManager = true,
                AnalyticsDashboard = true
            }
        };

        public ListingController(ApplicationDbContext context, ILogger<ListingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Admin Management

        /// <summary>
        /// Admin listing management page
        /// GET: /Listing/Admin
        /// </summary>
        [Route("Admin")]
        public async Task<IActionResult> Admin(string? status = null, string? plan = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.PremiumListings
                .Include(l => l.Cafe)
                .AsQueryable();

            // Filter by status
            if (status == "active")
            {
                query = query.Where(l => l.IsActive && l.EndDate > DateTime.UtcNow);
            }
            else if (status == "expired")
            {
                query = query.Where(l => l.EndDate <= DateTime.UtcNow);
            }
            else if (status == "inactive")
            {
                query = query.Where(l => !l.IsActive);
            }

            // Filter by plan
            if (!string.IsNullOrEmpty(plan))
            {
                query = query.Where(l => l.PlanType == plan);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var listings = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Stats
            ViewBag.TotalListings = await _context.PremiumListings.CountAsync();
            ViewBag.ActiveListings = await _context.PremiumListings.CountAsync(l => l.IsActive && l.EndDate > DateTime.UtcNow);
            ViewBag.ExpiredListings = await _context.PremiumListings.CountAsync(l => l.EndDate <= DateTime.UtcNow);
            ViewBag.TotalRevenue = await _context.PremiumListings.SumAsync(l => l.MonthlyFee);

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedPlan = plan;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.Plans = Plans;

            return View(listings);
        }

        /// <summary>
        /// Create new listing
        /// GET: /Listing/Create
        /// </summary>
        [Route("Create")]
        public async Task<IActionResult> Create(int? cafeId = null)
        {
            ViewBag.Plans = Plans;

            // Get cafes without active listings
            var cafesWithListings = await _context.PremiumListings
                .Where(l => l.IsActive && l.EndDate > DateTime.UtcNow)
                .Select(l => l.CafeId)
                .ToListAsync();

            var availableCafes = await _context.Cafes
                .Where(c => c.IsActive && !cafesWithListings.Contains(c.CafeId))
                .OrderBy(c => c.Name)
                .Select(c => new { c.CafeId, c.Name, c.City, c.Country })
                .ToListAsync();

            ViewBag.AvailableCafes = availableCafes;
            ViewBag.PreselectedCafeId = cafeId;

            return View(new PremiumListing
            {
                CafeId = cafeId ?? 0,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                PlanType = "Basic",
                MonthlyFee = Plans["Basic"].MonthlyPrice
            });
        }

        /// <summary>
        /// Save new listing
        /// POST: /Listing/Create
        /// </summary>
        [Route("Create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] PremiumListing listing)
        {
            try
            {
                // Validate cafe exists
                var cafe = await _context.Cafes.FindAsync(listing.CafeId);
                if (cafe == null)
                {
                    TempData["Error"] = "Cafe not found";
                    return RedirectToAction("Create");
                }

                // Check for existing active listing
                var existingListing = await _context.PremiumListings
                    .FirstOrDefaultAsync(l => l.CafeId == listing.CafeId && l.IsActive && l.EndDate > DateTime.UtcNow);

                if (existingListing != null)
                {
                    TempData["Error"] = "This cafe already has an active listing";
                    return RedirectToAction("Create");
                }

                // Set features based on plan
                if (Plans.TryGetValue(listing.PlanType, out var plan))
                {
                    listing.MonthlyFee = plan.MonthlyPrice;
                    listing.FeaturedPlacement = plan.FeaturedPlacement;
                    listing.PhotoGallery = plan.PhotoGallery;
                    listing.EventListings = plan.EventListings;
                    listing.GameInventoryManager = plan.GameInventoryManager;
                    listing.AnalyticsDashboard = plan.AnalyticsDashboard;
                }

                listing.CreatedAt = DateTime.UtcNow;
                listing.IsActive = true;

                // Update cafe IsPremium flag
                cafe.IsPremium = true;

                _context.PremiumListings.Add(listing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created listing for cafe {CafeName} (ID: {CafeId}), Plan: {Plan}",
                    cafe.Name, cafe.CafeId, listing.PlanType);

                TempData["Success"] = $"Listing created for {cafe.Name}!";
                return RedirectToAction("Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating listing");
                TempData["Error"] = $"Error creating listing: {ex.Message}";
                return RedirectToAction("Create");
            }
        }

        /// <summary>
        /// Edit listing
        /// GET: /Listing/Edit/{id}
        /// </summary>
        [Route("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var listing = await _context.PremiumListings
                .Include(l => l.Cafe)
                .FirstOrDefaultAsync(l => l.ListingId == id);

            if (listing == null)
            {
                return NotFound();
            }

            ViewBag.Plans = Plans;
            return View(listing);
        }

        /// <summary>
        /// Save listing changes
        /// POST: /Listing/Edit/{id}
        /// </summary>
        [Route("Edit/{id}")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromForm] PremiumListing listing)
        {
            try
            {
                var existingListing = await _context.PremiumListings
                    .Include(l => l.Cafe)
                    .FirstOrDefaultAsync(l => l.ListingId == id);

                if (existingListing == null)
                {
                    return NotFound();
                }

                // Update fields
                existingListing.PlanType = listing.PlanType;
                existingListing.StartDate = listing.StartDate;
                existingListing.EndDate = listing.EndDate;
                existingListing.IsActive = listing.IsActive;

                // Set features based on plan
                if (Plans.TryGetValue(listing.PlanType, out var plan))
                {
                    existingListing.MonthlyFee = plan.MonthlyPrice;
                    existingListing.FeaturedPlacement = plan.FeaturedPlacement;
                    existingListing.PhotoGallery = plan.PhotoGallery;
                    existingListing.EventListings = plan.EventListings;
                    existingListing.GameInventoryManager = plan.GameInventoryManager;
                    existingListing.AnalyticsDashboard = plan.AnalyticsDashboard;
                }

                // Update cafe IsPremium flag based on listing status
                if (existingListing.Cafe != null)
                {
                    existingListing.Cafe.IsPremium = existingListing.IsActive && existingListing.EndDate > DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated listing {ListingId} for cafe {CafeName}",
                    id, existingListing.Cafe?.Name);

                TempData["Success"] = "Listing updated successfully!";
                return RedirectToAction("Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating listing {ListingId}", id);
                TempData["Error"] = $"Error updating listing: {ex.Message}";
                return RedirectToAction("Edit", new { id });
            }
        }

        /// <summary>
        /// Delete listing
        /// POST: /Listing/Delete/{id}
        /// </summary>
        [Route("Delete/{id}")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var listing = await _context.PremiumListings
                    .Include(l => l.Cafe)
                    .FirstOrDefaultAsync(l => l.ListingId == id);

                if (listing == null)
                {
                    return Json(new { success = false, message = "Listing not found" });
                }

                // Update cafe IsPremium flag
                if (listing.Cafe != null)
                {
                    listing.Cafe.IsPremium = false;
                }

                _context.PremiumListings.Remove(listing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted listing {ListingId} for cafe {CafeName}",
                    id, listing.Cafe?.Name);

                return Json(new { success = true, message = "Listing deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting listing {ListingId}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Extend listing
        /// POST: /Listing/Extend/{id}
        /// </summary>
        [Route("Extend/{id}")]
        [HttpPost]
        public async Task<IActionResult> Extend(int id, int months = 1)
        {
            try
            {
                var listing = await _context.PremiumListings
                    .Include(l => l.Cafe)
                    .FirstOrDefaultAsync(l => l.ListingId == id);

                if (listing == null)
                {
                    return Json(new { success = false, message = "Listing not found" });
                }

                // Extend from current end date or now (whichever is later)
                var extendFrom = listing.EndDate > DateTime.UtcNow ? listing.EndDate : DateTime.UtcNow;
                listing.EndDate = extendFrom.AddMonths(months);
                listing.IsActive = true;

                // Update cafe IsPremium flag
                if (listing.Cafe != null)
                {
                    listing.Cafe.IsPremium = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Extended listing {ListingId} by {Months} months, new end date: {EndDate}",
                    id, months, listing.EndDate);

                return Json(new { success = true, message = $"Listing extended to {listing.EndDate:yyyy-MM-dd}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending listing {ListingId}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Public Pages

        /// <summary>
        /// Public pricing page
        /// GET: /Listing/Pricing
        /// </summary>
        [Route("Pricing")]
        public IActionResult Pricing()
        {
            ViewBag.Plans = Plans;
            return View();
        }

        /// <summary>
        /// Claim cafe form
        /// GET: /Listing/Claim/{cafeId}
        /// </summary>
        [Route("Claim/{cafeId}")]
        public async Task<IActionResult> Claim(int cafeId, string? plan = null)
        {
            var cafe = await _context.Cafes.FindAsync(cafeId);
            if (cafe == null)
            {
                return NotFound();
            }

            // Check if already has active listing
            var existingListing = await _context.PremiumListings
                .FirstOrDefaultAsync(l => l.CafeId == cafeId && l.IsActive && l.EndDate > DateTime.UtcNow);

            if (existingListing != null)
            {
                TempData["Error"] = "This cafe already has an active listing";
                return RedirectToAction("Pricing");
            }

            ViewBag.Cafe = cafe;
            ViewBag.Plans = Plans;
            ViewBag.SelectedPlan = plan ?? "Premium";

            return View();
        }

        #endregion
    }

    // Helper class for plan definitions
    public class ListingPlan
    {
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
        public bool FeaturedPlacement { get; set; }
        public bool PhotoGallery { get; set; }
        public bool EventListings { get; set; }
        public bool GameInventoryManager { get; set; }
        public bool AnalyticsDashboard { get; set; }
    }
}
