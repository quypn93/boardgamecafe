using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using BoardGameCafeFinder.Services;
using Stripe;
using Stripe.Checkout;
using System.ComponentModel.DataAnnotations;

namespace BoardGameCafeFinder.Controllers
{
    [Route("[controller]")]
    public class ListingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ListingController> _logger;
        private readonly IPaymentService _paymentService;
        private readonly IPaymentServiceFactory _paymentServiceFactory;
        private readonly IStripeService _stripeService; // Legacy support
        private readonly IConfiguration _configuration;

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

        // Duration discounts
        private static readonly Dictionary<int, decimal> DurationDiscounts = new()
        {
            { 1, 0m },
            { 3, 0.05m },
            { 6, 0.10m },
            { 12, 0.20m }
        };

        public ListingController(
            ApplicationDbContext context,
            ILogger<ListingController> logger,
            IPaymentService paymentService,
            IPaymentServiceFactory paymentServiceFactory,
            IStripeService stripeService,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _paymentService = paymentService;
            _paymentServiceFactory = paymentServiceFactory;
            _stripeService = stripeService; // Legacy support for webhook
            _configuration = configuration;
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
            ViewBag.TotalRevenue = await _context.Invoices.Where(i => i.PaymentStatus == "paid").SumAsync(i => i.TotalAmount);

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedPlan = plan;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.Plans = Plans;

            return View(listings);
        }

        /// <summary>
        /// Admin claims management page
        /// GET: /Listing/Claims
        /// </summary>
        [Route("Claims")]
        public async Task<IActionResult> Claims(string? status = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.ClaimRequests
                .Include(c => c.Cafe)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.PaymentStatus == status);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var claims = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(claims);
        }

        /// <summary>
        /// Create new listing (admin)
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
        /// Save new listing (admin)
        /// POST: /Listing/Create
        /// </summary>
        [Route("Create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] PremiumListing listing)
        {
            try
            {
                var cafe = await _context.Cafes.FindAsync(listing.CafeId);
                if (cafe == null)
                {
                    TempData["Error"] = "Cafe not found";
                    return RedirectToAction("Create");
                }

                var existingListing = await _context.PremiumListings
                    .FirstOrDefaultAsync(l => l.CafeId == listing.CafeId && l.IsActive && l.EndDate > DateTime.UtcNow);

                if (existingListing != null)
                {
                    TempData["Error"] = "This cafe already has an active listing";
                    return RedirectToAction("Create");
                }

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

            if (listing == null) return NotFound();

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

                if (existingListing == null) return NotFound();

                existingListing.PlanType = listing.PlanType;
                existingListing.StartDate = listing.StartDate;
                existingListing.EndDate = listing.EndDate;
                existingListing.IsActive = listing.IsActive;

                if (Plans.TryGetValue(listing.PlanType, out var plan))
                {
                    existingListing.MonthlyFee = plan.MonthlyPrice;
                    existingListing.FeaturedPlacement = plan.FeaturedPlacement;
                    existingListing.PhotoGallery = plan.PhotoGallery;
                    existingListing.EventListings = plan.EventListings;
                    existingListing.GameInventoryManager = plan.GameInventoryManager;
                    existingListing.AnalyticsDashboard = plan.AnalyticsDashboard;
                }

                if (existingListing.Cafe != null)
                {
                    existingListing.Cafe.IsPremium = existingListing.IsActive && existingListing.EndDate > DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
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
                    return Json(new { success = false, message = "Listing not found" });

                if (listing.Cafe != null)
                    listing.Cafe.IsPremium = false;

                _context.PremiumListings.Remove(listing);
                await _context.SaveChangesAsync();

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
                    return Json(new { success = false, message = "Listing not found" });

                var extendFrom = listing.EndDate > DateTime.UtcNow ? listing.EndDate : DateTime.UtcNow;
                listing.EndDate = extendFrom.AddMonths(months);
                listing.IsActive = true;

                if (listing.Cafe != null)
                    listing.Cafe.IsPremium = true;

                await _context.SaveChangesAsync();
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
        /// Submit your cafe form (for cafe owners)
        /// GET: /Listing/SubmitCafe
        /// </summary>
        [Route("SubmitCafe")]
        public IActionResult SubmitCafe(string? plan = null)
        {
            ViewBag.Plans = Plans;
            ViewBag.SelectedPlan = plan;
            return View();
        }

        /// <summary>
        /// Process cafe submission
        /// POST: /Listing/SubmitCafe
        /// </summary>
        [Route("SubmitCafe")]
        [HttpPost]
        public async Task<IActionResult> SubmitCafe([FromForm] CafeSubmissionModel model)
        {
            try
            {
                // Check if cafe with same name/address already exists
                var existingCafe = await _context.Cafes
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == model.CafeName.ToLower() &&
                                              c.City.ToLower() == model.City.ToLower());

                if (existingCafe != null)
                {
                    TempData["Error"] = "A cafe with this name already exists in this city. If this is your cafe, please use the Claim feature instead.";
                    ViewBag.Plans = Plans;
                    ViewBag.SelectedPlan = model.PlanType;
                    return View(model);
                }

                // Create the cafe
                var cafe = new Cafe
                {
                    Name = model.CafeName,
                    Description = model.Description,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Country = model.Country ?? "United States",
                    PostalCode = model.PostalCode,
                    Phone = model.Phone,
                    Email = model.Email,
                    Website = model.Website,
                    Latitude = model.Latitude ?? 0,
                    Longitude = model.Longitude ?? 0,
                    IsActive = false, // Pending approval
                    IsPremium = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Generate slug
                cafe.Slug = GenerateSlug(cafe.Name, cafe.City);

                _context.Cafes.Add(cafe);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New cafe submitted: {CafeName} in {City} by {OwnerName}",
                    cafe.Name, cafe.City, model.OwnerName);

                // If user selected a plan, redirect to claim/checkout
                if (!string.IsNullOrEmpty(model.PlanType) && model.PlanType != "Free")
                {
                    return RedirectToAction("Claim", new { cafeId = cafe.CafeId, plan = model.PlanType });
                }

                // Otherwise, show success page
                TempData["Success"] = "Your cafe has been submitted for review. We'll notify you once it's approved.";
                return RedirectToAction("SubmitCafeSuccess", new { cafeId = cafe.CafeId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting cafe");
                TempData["Error"] = "An error occurred while submitting your cafe. Please try again.";
                ViewBag.Plans = Plans;
                ViewBag.SelectedPlan = model.PlanType;
                return View(model);
            }
        }

        /// <summary>
        /// Cafe submission success page
        /// GET: /Listing/SubmitCafeSuccess
        /// </summary>
        [Route("SubmitCafeSuccess/{cafeId}")]
        public async Task<IActionResult> SubmitCafeSuccess(int cafeId)
        {
            var cafe = await _context.Cafes.FindAsync(cafeId);
            ViewBag.Cafe = cafe;
            ViewBag.Plans = Plans;
            return View();
        }

        private string GenerateSlug(string name, string city)
        {
            var slug = $"{name}-{city}".ToLower()
                .Replace(" ", "-")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("&", "and");

            // Remove special characters
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            return slug.Trim('-');
        }

        /// <summary>
        /// Claim cafe form
        /// GET: /Listing/Claim/{cafeId}
        /// </summary>
        [Route("Claim/{cafeId}")]
        public async Task<IActionResult> Claim(int cafeId, string? plan = null)
        {
            var cafe = await _context.Cafes.FindAsync(cafeId);
            if (cafe == null) return NotFound();

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
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];

            return View();
        }

        #endregion

        #region Payment Flow

        /// <summary>
        /// Submit claim and redirect to Stripe checkout
        /// POST: /Listing/SubmitClaim
        /// </summary>
        [Route("SubmitClaim")]
        [HttpPost]
        public async Task<IActionResult> SubmitClaim([FromForm] ClaimFormModel model)
        {
            try
            {
                // Validate cafe
                var cafe = await _context.Cafes.FindAsync(model.CafeId);
                if (cafe == null)
                {
                    TempData["Error"] = "Cafe not found";
                    return RedirectToAction("Pricing");
                }

                // Check existing listing
                var existingListing = await _context.PremiumListings
                    .FirstOrDefaultAsync(l => l.CafeId == model.CafeId && l.IsActive && l.EndDate > DateTime.UtcNow);

                if (existingListing != null)
                {
                    TempData["Error"] = "This cafe already has an active listing";
                    return RedirectToAction("Pricing");
                }

                // Get plan details
                if (!Plans.TryGetValue(model.PlanType, out var plan))
                {
                    TempData["Error"] = "Invalid plan selected";
                    return RedirectToAction("Claim", new { cafeId = model.CafeId });
                }

                // Calculate pricing
                var discount = DurationDiscounts.GetValueOrDefault(model.DurationMonths, 0m);
                var subtotal = plan.MonthlyPrice * model.DurationMonths;
                var discountAmount = subtotal * discount;
                var total = subtotal - discountAmount;

                // Create claim request
                var claimRequest = new ClaimRequest
                {
                    CafeId = model.CafeId,
                    ContactName = model.ContactName,
                    ContactEmail = model.ContactEmail,
                    ContactPhone = model.ContactPhone,
                    ContactRole = model.ContactRole,
                    PlanType = model.PlanType,
                    DurationMonths = model.DurationMonths,
                    MonthlyRate = plan.MonthlyPrice,
                    DiscountPercent = discount * 100,
                    TotalAmount = total,
                    VerificationMethod = model.VerificationMethod,
                    PaymentStatus = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.ClaimRequests.Add(claimRequest);
                await _context.SaveChangesAsync();

                // Create checkout session using active payment provider
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                // PayPal uses token param, Stripe uses session_id with placeholder
                var successUrl = _paymentService.ProviderName == "Stripe"
                    ? $"{baseUrl}/Listing/CheckoutSuccess?session_id={{CHECKOUT_SESSION_ID}}"
                    : $"{baseUrl}/Listing/CheckoutSuccess?claim_id={claimRequest.ClaimRequestId}";
                var cancelUrl = $"{baseUrl}/Listing/CheckoutCancel?claim_id={claimRequest.ClaimRequestId}";

                var result = await _paymentService.CreateCheckoutSessionAsync(claimRequest, successUrl, cancelUrl);

                if (!result.Success)
                {
                    TempData["Error"] = $"Payment error: {result.ErrorMessage}";
                    return RedirectToAction("Claim", new { cafeId = model.CafeId });
                }

                // Update claim with session ID
                claimRequest.StripeSessionId = result.SessionId; // Used for both Stripe and PayPal
                claimRequest.PaymentStatus = "processing";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created claim request {ClaimId} for cafe {CafeName}, redirecting to {Provider}",
                    claimRequest.ClaimRequestId, cafe.Name, _paymentService.ProviderName);

                // Redirect to checkout
                return Redirect(result.CheckoutUrl!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing claim submission");
                TempData["Error"] = $"Error processing your request: {ex.Message}";
                return RedirectToAction("Claim", new { cafeId = model.CafeId });
            }
        }

        /// <summary>
        /// Checkout success - called after successful payment
        /// GET: /Listing/CheckoutSuccess
        /// Supports both Stripe (session_id) and PayPal (token, PayerID, claim_id)
        /// </summary>
        [Route("CheckoutSuccess")]
        public async Task<IActionResult> CheckoutSuccess(
            string? session_id = null,
            string? token = null,
            string? PayerID = null,
            int? claim_id = null)
        {
            try
            {
                ClaimRequest? claimRequest = null;
                string? sessionId = session_id ?? token; // PayPal uses 'token' as order ID

                // Find claim request
                if (!string.IsNullOrEmpty(sessionId))
                {
                    claimRequest = await _context.ClaimRequests
                        .Include(c => c.Cafe)
                        .FirstOrDefaultAsync(c => c.StripeSessionId == sessionId);
                }
                else if (claim_id.HasValue)
                {
                    claimRequest = await _context.ClaimRequests
                        .Include(c => c.Cafe)
                        .FirstOrDefaultAsync(c => c.ClaimRequestId == claim_id.Value);
                    sessionId = claimRequest?.StripeSessionId;
                }

                if (claimRequest == null || string.IsNullOrEmpty(sessionId))
                {
                    TempData["Error"] = "Claim request not found";
                    return RedirectToAction("Pricing");
                }

                // If already processed, show success
                if (claimRequest.PaymentStatus == "completed")
                {
                    ViewBag.ClaimRequest = claimRequest;
                    ViewBag.Invoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.ClaimRequestId == claimRequest.ClaimRequestId);
                    ViewBag.PaymentProvider = _paymentService.ProviderName;
                    return View();
                }

                // Get session details from payment provider
                var sessionDetails = await _paymentService.GetSessionAsync(sessionId);
                if (sessionDetails == null)
                {
                    TempData["Error"] = "Payment session not found";
                    return RedirectToAction("Pricing");
                }

                // For PayPal, we need to capture the payment
                if (_paymentService.ProviderName == "PayPal" && !string.IsNullOrEmpty(PayerID))
                {
                    var captured = await _paymentService.CapturePaymentAsync(sessionId, PayerID);
                    if (captured)
                    {
                        sessionDetails = await _paymentService.GetSessionAsync(sessionId);
                    }
                }

                // Process payment if completed
                if (sessionDetails?.Status == "completed")
                {
                    await ProcessSuccessfulPaymentGeneric(claimRequest, sessionDetails);
                }

                ViewBag.ClaimRequest = claimRequest;
                ViewBag.Invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.ClaimRequestId == claimRequest.ClaimRequestId);
                ViewBag.PaymentProvider = _paymentService.ProviderName;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing checkout success");
                TempData["Error"] = "Error processing your payment. Please contact support.";
                return RedirectToAction("Pricing");
            }
        }

        /// <summary>
        /// Checkout cancelled
        /// GET: /Listing/CheckoutCancel
        /// </summary>
        [Route("CheckoutCancel")]
        public async Task<IActionResult> CheckoutCancel(int? claim_id)
        {
            if (claim_id.HasValue)
            {
                var claimRequest = await _context.ClaimRequests
                    .Include(c => c.Cafe)
                    .FirstOrDefaultAsync(c => c.ClaimRequestId == claim_id.Value);

                if (claimRequest != null)
                {
                    claimRequest.PaymentStatus = "cancelled";
                    claimRequest.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    ViewBag.ClaimRequest = claimRequest;
                }
            }

            return View();
        }

        /// <summary>
        /// Stripe webhook handler
        /// POST: /Listing/Webhook
        /// </summary>
        [Route("Webhook")]
        [HttpPost]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var signature = Request.Headers["Stripe-Signature"];
                var webhookSecret = _configuration["Stripe:WebhookSecret"];

                var stripeEvent = _stripeService.ConstructWebhookEvent(json, signature!, webhookSecret!);

                _logger.LogInformation("Received Stripe webhook: {EventType}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        var session = stripeEvent.Data.Object as Session;
                        await HandleCheckoutSessionCompleted(session!);
                        break;

                    case "payment_intent.succeeded":
                        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                        _logger.LogInformation("Payment succeeded: {PaymentIntentId}", paymentIntent?.Id);
                        break;

                    case "payment_intent.payment_failed":
                        var failedIntent = stripeEvent.Data.Object as PaymentIntent;
                        await HandlePaymentFailed(failedIntent!);
                        break;
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                return BadRequest();
            }
        }

        #endregion

        #region Invoice

        /// <summary>
        /// View invoice
        /// GET: /Listing/Invoice/{id}
        /// </summary>
        [Route("Invoice/{id}")]
        public async Task<IActionResult> Invoice(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Cafe)
                .Include(i => i.ClaimRequest)
                .Include(i => i.PremiumListing)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        /// <summary>
        /// Admin invoices list
        /// GET: /Listing/Invoices
        /// </summary>
        [Route("Invoices")]
        public async Task<IActionResult> Invoices(string? status = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Invoices
                .Include(i => i.Cafe)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.PaymentStatus == status);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalRevenue = await _context.Invoices.Where(i => i.PaymentStatus == "paid").SumAsync(i => i.TotalAmount);
            ViewBag.SelectedStatus = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(invoices);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Process successful payment - provider agnostic version
        /// </summary>
        private async Task ProcessSuccessfulPaymentGeneric(ClaimRequest claimRequest, PaymentSessionDetails sessionDetails)
        {
            // Update claim request
            claimRequest.PaymentStatus = "completed";
            claimRequest.PaidAt = DateTime.UtcNow;
            claimRequest.StripePaymentIntentId = sessionDetails.PaymentId; // Works for both Stripe and PayPal
            claimRequest.UpdatedAt = DateTime.UtcNow;

            // Get plan features
            Plans.TryGetValue(claimRequest.PlanType, out var plan);

            // Create premium listing
            var listing = new PremiumListing
            {
                CafeId = claimRequest.CafeId,
                PlanType = claimRequest.PlanType,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(claimRequest.DurationMonths),
                MonthlyFee = claimRequest.MonthlyRate,
                IsActive = true,
                FeaturedPlacement = plan?.FeaturedPlacement ?? false,
                PhotoGallery = plan?.PhotoGallery ?? false,
                EventListings = plan?.EventListings ?? false,
                GameInventoryManager = plan?.GameInventoryManager ?? false,
                AnalyticsDashboard = plan?.AnalyticsDashboard ?? false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PremiumListings.Add(listing);
            await _context.SaveChangesAsync();

            claimRequest.PremiumListingId = listing.ListingId;

            // Update cafe
            var cafe = await _context.Cafes.FindAsync(claimRequest.CafeId);
            if (cafe != null)
            {
                cafe.IsPremium = true;
            }

            // Create invoice
            var invoiceCount = await _context.Invoices.CountAsync() + 1;
            var invoice = new Models.Domain.Invoice
            {
                InvoiceNumber = Models.Domain.Invoice.GenerateInvoiceNumber(invoiceCount),
                ClaimRequestId = claimRequest.ClaimRequestId,
                PremiumListingId = listing.ListingId,
                CafeId = claimRequest.CafeId,
                BillingName = claimRequest.ContactName,
                BillingEmail = claimRequest.ContactEmail,
                Description = $"{claimRequest.PlanType} Plan - {claimRequest.DurationMonths} month(s)",
                PlanType = claimRequest.PlanType,
                PeriodMonths = claimRequest.DurationMonths,
                PeriodStart = listing.StartDate,
                PeriodEnd = listing.EndDate,
                Subtotal = claimRequest.MonthlyRate * claimRequest.DurationMonths,
                DiscountAmount = (claimRequest.MonthlyRate * claimRequest.DurationMonths) * (claimRequest.DiscountPercent / 100),
                TaxAmount = 0,
                TotalAmount = claimRequest.TotalAmount,
                PaymentStatus = "paid",
                PaymentMethod = _paymentService.ProviderName.ToLower(),
                StripePaymentIntentId = sessionDetails.PaymentId,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment processed via {Provider} for ClaimRequest {ClaimId}. Created Listing {ListingId} and Invoice {InvoiceNumber}",
                _paymentService.ProviderName, claimRequest.ClaimRequestId, listing.ListingId, invoice.InvoiceNumber);
        }

        /// <summary>
        /// Legacy Stripe-specific version for webhook compatibility
        /// </summary>
        private async Task ProcessSuccessfulPayment(ClaimRequest claimRequest, Session session)
        {
            var sessionDetails = new PaymentSessionDetails
            {
                SessionId = session.Id,
                Status = "completed",
                PaymentId = session.PaymentIntentId,
                AmountPaid = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : null,
                Currency = session.Currency,
                CustomerEmail = session.CustomerEmail
            };
            await ProcessSuccessfulPaymentGeneric(claimRequest, sessionDetails);
        }

        private async Task HandleCheckoutSessionCompleted(Session session)
        {
            var claimRequest = await _context.ClaimRequests
                .Include(c => c.Cafe)
                .FirstOrDefaultAsync(c => c.StripeSessionId == session.Id);

            if (claimRequest == null)
            {
                _logger.LogWarning("Claim request not found for session {SessionId}", session.Id);
                return;
            }

            if (claimRequest.PaymentStatus == "completed")
            {
                _logger.LogInformation("Claim {ClaimId} already processed", claimRequest.ClaimRequestId);
                return;
            }

            if (session.PaymentStatus == "paid")
            {
                await ProcessSuccessfulPayment(claimRequest, session);
            }
        }

        private async Task HandlePaymentFailed(PaymentIntent paymentIntent)
        {
            var claimRequest = await _context.ClaimRequests
                .FirstOrDefaultAsync(c => c.StripePaymentIntentId == paymentIntent.Id);

            if (claimRequest != null)
            {
                claimRequest.PaymentStatus = "failed";
                claimRequest.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogWarning("Payment failed for ClaimRequest {ClaimId}", claimRequest.ClaimRequestId);
            }
        }

        #endregion
    }

    // Form model for claim submission
    public class ClaimFormModel
    {
        public int CafeId { get; set; }
        public string PlanType { get; set; } = "Premium";
        public int DurationMonths { get; set; } = 3;
        public string ContactName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string ContactRole { get; set; } = "Owner";
        public string VerificationMethod { get; set; } = "email";
        public bool AgreeTerms { get; set; }
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

    // Form model for cafe submission
    public class CafeSubmissionModel
    {
        // Cafe Information
        [Required(ErrorMessage = "Cafe name is required")]
        [MaxLength(200)]
        public string CafeName { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Url]
        [MaxLength(500)]
        public string? Website { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Owner Information
        [Required(ErrorMessage = "Your name is required")]
        [MaxLength(200)]
        public string OwnerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        [MaxLength(200)]
        public string OwnerEmail { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? OwnerPhone { get; set; }

        [MaxLength(50)]
        public string OwnerRole { get; set; } = "Owner";

        // Plan Selection
        public string? PlanType { get; set; } // null or "Free" = free listing, "Basic"/"Premium"/"Featured" = paid

        public bool AgreeTerms { get; set; }
    }
}
