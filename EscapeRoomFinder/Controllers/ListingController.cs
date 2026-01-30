using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Services;
using Stripe;
using Stripe.Checkout;
using System.ComponentModel.DataAnnotations;

namespace EscapeRoomFinder.Controllers
{
    [Route("[controller]")]
    public class ListingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ListingController> _logger;
        private readonly IPaymentService _paymentService;
        private readonly IPaymentServiceFactory _paymentServiceFactory;
        private readonly IStripeService _stripeService;
        private readonly IConfiguration _configuration;

        // Plan definitions for Escape Room venues
        public static readonly Dictionary<string, ListingPlan> Plans = new()
        {
            ["Basic"] = new ListingPlan
            {
                Name = "Basic",
                MonthlyPrice = 29.99m,
                Features = new[] { "Basic listing", "Contact information", "Business hours", "Location on map" },
                FeaturedPlacement = false,
                PhotoGallery = false,
                RoomShowcase = false,
                BookingIntegration = false,
                AnalyticsDashboard = false
            },
            ["Premium"] = new ListingPlan
            {
                Name = "Premium",
                MonthlyPrice = 59.99m,
                Features = new[] { "Everything in Basic", "Photo gallery (up to 20)", "Room showcase", "Priority in search results" },
                FeaturedPlacement = false,
                PhotoGallery = true,
                RoomShowcase = true,
                BookingIntegration = false,
                AnalyticsDashboard = false
            },
            ["Featured"] = new ListingPlan
            {
                Name = "Featured",
                MonthlyPrice = 99.99m,
                Features = new[] { "Everything in Premium", "Featured placement on homepage", "Booking integration", "Analytics dashboard", "Unlimited photos" },
                FeaturedPlacement = true,
                PhotoGallery = true,
                RoomShowcase = true,
                BookingIntegration = true,
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
            _stripeService = stripeService;
            _configuration = configuration;
        }

        #region Admin Management

        [Route("Admin")]
        public async Task<IActionResult> Admin(string? status = null, string? plan = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.PremiumListings
                .Include(l => l.Venue)
                .AsQueryable();

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

            ViewBag.TotalListings = await _context.PremiumListings.CountAsync();
            ViewBag.ActiveListings = await _context.PremiumListings.CountAsync(l => l.IsActive && l.EndDate > DateTime.UtcNow);
            ViewBag.ExpiredListings = await _context.PremiumListings.CountAsync(l => l.EndDate <= DateTime.UtcNow);
            ViewBag.TotalRevenue = await _context.Invoices.Where(i => i.Status == "paid").SumAsync(i => i.Amount);

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedPlan = plan;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.Plans = Plans;

            return View(listings);
        }

        [Route("Claims")]
        public async Task<IActionResult> Claims(string? status = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.ClaimRequests
                .Include(c => c.Venue)
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

        [Route("Create")]
        public async Task<IActionResult> Create(int? venueId = null)
        {
            ViewBag.Plans = Plans;

            var venuesWithListings = await _context.PremiumListings
                .Where(l => l.IsActive && l.EndDate > DateTime.UtcNow)
                .Select(l => l.VenueId)
                .ToListAsync();

            var availableVenues = await _context.Venues
                .Where(v => v.IsActive && !venuesWithListings.Contains(v.VenueId))
                .OrderBy(v => v.Name)
                .Select(v => new { v.VenueId, v.Name, v.City, v.Country })
                .ToListAsync();

            ViewBag.AvailableVenues = availableVenues;
            ViewBag.PreselectedVenueId = venueId;

            return View(new PremiumListing
            {
                VenueId = venueId ?? 0,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                PlanType = "Basic",
                MonthlyFee = Plans["Basic"].MonthlyPrice
            });
        }

        [Route("Create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] PremiumListing listing)
        {
            try
            {
                var venue = await _context.Venues.FindAsync(listing.VenueId);
                if (venue == null)
                {
                    TempData["Error"] = "Venue not found";
                    return RedirectToAction("Create");
                }

                var existingListing = await _context.PremiumListings
                    .FirstOrDefaultAsync(l => l.VenueId == listing.VenueId && l.IsActive && l.EndDate > DateTime.UtcNow);

                if (existingListing != null)
                {
                    TempData["Error"] = "This venue already has an active listing";
                    return RedirectToAction("Create");
                }

                if (Plans.TryGetValue(listing.PlanType, out var plan))
                {
                    listing.MonthlyFee = plan.MonthlyPrice;
                    listing.FeaturedPlacement = plan.FeaturedPlacement;
                    listing.PhotoGallery = plan.PhotoGallery;
                    listing.RoomShowcase = plan.RoomShowcase;
                    listing.BookingIntegration = plan.BookingIntegration;
                    listing.AnalyticsDashboard = plan.AnalyticsDashboard;
                }

                listing.CreatedAt = DateTime.UtcNow;
                listing.IsActive = true;
                venue.IsPremium = true;

                _context.PremiumListings.Add(listing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created listing for venue {VenueName} (ID: {VenueId}), Plan: {Plan}",
                    venue.Name, venue.VenueId, listing.PlanType);

                TempData["Success"] = $"Listing created for {venue.Name}!";
                return RedirectToAction("Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating listing");
                TempData["Error"] = $"Error creating listing: {ex.Message}";
                return RedirectToAction("Create");
            }
        }

        [Route("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var listing = await _context.PremiumListings
                .Include(l => l.Venue)
                .FirstOrDefaultAsync(l => l.ListingId == id);

            if (listing == null) return NotFound();

            ViewBag.Plans = Plans;
            return View(listing);
        }

        [Route("Edit/{id}")]
        [HttpPost]
        public async Task<IActionResult> Edit(int id, [FromForm] PremiumListing listing)
        {
            try
            {
                var existingListing = await _context.PremiumListings
                    .Include(l => l.Venue)
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
                    existingListing.RoomShowcase = plan.RoomShowcase;
                    existingListing.BookingIntegration = plan.BookingIntegration;
                    existingListing.AnalyticsDashboard = plan.AnalyticsDashboard;
                }

                if (existingListing.Venue != null)
                {
                    existingListing.Venue.IsPremium = existingListing.IsActive && existingListing.EndDate > DateTime.UtcNow;
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

        [Route("Delete/{id}")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var listing = await _context.PremiumListings
                    .Include(l => l.Venue)
                    .FirstOrDefaultAsync(l => l.ListingId == id);

                if (listing == null)
                    return Json(new { success = false, message = "Listing not found" });

                if (listing.Venue != null)
                    listing.Venue.IsPremium = false;

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

        [Route("Extend/{id}")]
        [HttpPost]
        public async Task<IActionResult> Extend(int id, int months = 1)
        {
            try
            {
                var listing = await _context.PremiumListings
                    .Include(l => l.Venue)
                    .FirstOrDefaultAsync(l => l.ListingId == id);

                if (listing == null)
                    return Json(new { success = false, message = "Listing not found" });

                var extendFrom = listing.EndDate > DateTime.UtcNow ? listing.EndDate : DateTime.UtcNow;
                listing.EndDate = extendFrom.AddMonths(months);
                listing.IsActive = true;

                if (listing.Venue != null)
                    listing.Venue.IsPremium = true;

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

        [Route("Pricing")]
        public IActionResult Pricing()
        {
            ViewBag.Plans = Plans;
            return View();
        }

        [Route("SubmitVenue")]
        public IActionResult SubmitVenue(string? plan = null)
        {
            ViewBag.Plans = Plans;
            ViewBag.SelectedPlan = plan;
            return View();
        }

        [Route("SubmitVenue")]
        [HttpPost]
        public async Task<IActionResult> SubmitVenue([FromForm] VenueSubmissionModel model)
        {
            try
            {
                var existingVenue = await _context.Venues
                    .FirstOrDefaultAsync(v => v.Name.ToLower() == model.VenueName.ToLower() &&
                                              v.City.ToLower() == model.City.ToLower());

                if (existingVenue != null)
                {
                    TempData["Error"] = "A venue with this name already exists in this city. If this is your venue, please use the Claim feature instead.";
                    ViewBag.Plans = Plans;
                    ViewBag.SelectedPlan = model.PlanType;
                    return View(model);
                }

                var venue = new EscapeRoomVenue
                {
                    Name = model.VenueName,
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
                    IsActive = false,
                    IsPremium = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                venue.Slug = GenerateSlug(venue.Name, venue.City);

                _context.Venues.Add(venue);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New venue submitted: {VenueName} in {City} by {OwnerName}",
                    venue.Name, venue.City, model.OwnerName);

                if (!string.IsNullOrEmpty(model.PlanType) && model.PlanType != "Free")
                {
                    return RedirectToAction("Claim", new { venueId = venue.VenueId, plan = model.PlanType });
                }

                TempData["Success"] = "Your venue has been submitted for review. We'll notify you once it's approved.";
                return RedirectToAction("SubmitVenueSuccess", new { venueId = venue.VenueId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting venue");
                TempData["Error"] = "An error occurred while submitting your venue. Please try again.";
                ViewBag.Plans = Plans;
                ViewBag.SelectedPlan = model.PlanType;
                return View(model);
            }
        }

        [Route("SubmitVenueSuccess/{venueId}")]
        public async Task<IActionResult> SubmitVenueSuccess(int venueId)
        {
            var venue = await _context.Venues.FindAsync(venueId);
            ViewBag.Venue = venue;
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

            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

            return slug.Trim('-');
        }

        [Route("Claim/{venueId}")]
        public async Task<IActionResult> Claim(int venueId, string? plan = null)
        {
            var venue = await _context.Venues.FindAsync(venueId);
            if (venue == null) return NotFound();

            var existingListing = await _context.PremiumListings
                .FirstOrDefaultAsync(l => l.VenueId == venueId && l.IsActive && l.EndDate > DateTime.UtcNow);

            if (existingListing != null)
            {
                TempData["Error"] = "This venue already has an active listing";
                return RedirectToAction("Pricing");
            }

            ViewBag.Venue = venue;
            ViewBag.Plans = Plans;
            ViewBag.SelectedPlan = plan ?? "Premium";
            ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];

            return View();
        }

        #endregion

        #region Payment Flow

        [Route("SubmitClaim")]
        [HttpPost]
        public async Task<IActionResult> SubmitClaim([FromForm] ClaimFormModel model)
        {
            try
            {
                var venue = await _context.Venues.FindAsync(model.VenueId);
                if (venue == null)
                {
                    TempData["Error"] = "Venue not found";
                    return RedirectToAction("Pricing");
                }

                var existingListing = await _context.PremiumListings
                    .FirstOrDefaultAsync(l => l.VenueId == model.VenueId && l.IsActive && l.EndDate > DateTime.UtcNow);

                if (existingListing != null)
                {
                    TempData["Error"] = "This venue already has an active listing";
                    return RedirectToAction("Pricing");
                }

                if (!Plans.TryGetValue(model.PlanType, out var plan))
                {
                    TempData["Error"] = "Invalid plan selected";
                    return RedirectToAction("Claim", new { venueId = model.VenueId });
                }

                var discount = DurationDiscounts.GetValueOrDefault(model.DurationMonths, 0m);
                var subtotal = plan.MonthlyPrice * model.DurationMonths;
                var discountAmount = subtotal * discount;
                var total = subtotal - discountAmount;

                var claimRequest = new ClaimRequest
                {
                    VenueId = model.VenueId,
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

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var successUrl = _paymentService.ProviderName == "Stripe"
                    ? $"{baseUrl}/Listing/CheckoutSuccess?session_id={{CHECKOUT_SESSION_ID}}"
                    : $"{baseUrl}/Listing/CheckoutSuccess?claim_id={claimRequest.ClaimRequestId}";
                var cancelUrl = $"{baseUrl}/Listing/CheckoutCancel?claim_id={claimRequest.ClaimRequestId}";

                var result = await _paymentService.CreateCheckoutSessionAsync(claimRequest, successUrl, cancelUrl);

                if (!result.Success)
                {
                    TempData["Error"] = $"Payment error: {result.ErrorMessage}";
                    return RedirectToAction("Claim", new { venueId = model.VenueId });
                }

                claimRequest.StripeSessionId = result.SessionId;
                claimRequest.PaymentStatus = "processing";
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created claim request {ClaimId} for venue {VenueName}, redirecting to {Provider}",
                    claimRequest.ClaimRequestId, venue.Name, _paymentService.ProviderName);

                return Redirect(result.CheckoutUrl!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing claim submission");
                TempData["Error"] = $"Error processing your request: {ex.Message}";
                return RedirectToAction("Claim", new { venueId = model.VenueId });
            }
        }

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
                string? sessionId = session_id ?? token;

                if (!string.IsNullOrEmpty(sessionId))
                {
                    claimRequest = await _context.ClaimRequests
                        .Include(c => c.Venue)
                        .FirstOrDefaultAsync(c => c.StripeSessionId == sessionId);
                }
                else if (claim_id.HasValue)
                {
                    claimRequest = await _context.ClaimRequests
                        .Include(c => c.Venue)
                        .FirstOrDefaultAsync(c => c.ClaimRequestId == claim_id.Value);
                    sessionId = claimRequest?.StripeSessionId;
                }

                if (claimRequest == null || string.IsNullOrEmpty(sessionId))
                {
                    TempData["Error"] = "Claim request not found";
                    return RedirectToAction("Pricing");
                }

                if (claimRequest.PaymentStatus == "completed")
                {
                    ViewBag.ClaimRequest = claimRequest;
                    ViewBag.Invoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.ClaimRequestId == claimRequest.ClaimRequestId);
                    ViewBag.PaymentProvider = _paymentService.ProviderName;
                    return View();
                }

                var sessionDetails = await _paymentService.GetSessionAsync(sessionId);
                if (sessionDetails == null)
                {
                    TempData["Error"] = "Payment session not found";
                    return RedirectToAction("Pricing");
                }

                if (_paymentService.ProviderName == "PayPal" && !string.IsNullOrEmpty(PayerID))
                {
                    var captured = await _paymentService.CapturePaymentAsync(sessionId, PayerID);
                    if (captured)
                    {
                        sessionDetails = await _paymentService.GetSessionAsync(sessionId);
                    }
                }

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

        [Route("CheckoutCancel")]
        public async Task<IActionResult> CheckoutCancel(int? claim_id)
        {
            if (claim_id.HasValue)
            {
                var claimRequest = await _context.ClaimRequests
                    .Include(c => c.Venue)
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

        [Route("Invoice/{id}")]
        public async Task<IActionResult> Invoice(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Venue)
                .Include(i => i.ClaimRequest)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        [Route("Invoices")]
        public async Task<IActionResult> Invoices(string? status = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Invoices
                .Include(i => i.Venue)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalRevenue = await _context.Invoices.Where(i => i.Status == "paid").SumAsync(i => i.Amount);
            ViewBag.SelectedStatus = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(invoices);
        }

        #endregion

        #region Private Methods

        private async Task ProcessSuccessfulPaymentGeneric(ClaimRequest claimRequest, PaymentSessionDetails sessionDetails)
        {
            claimRequest.PaymentStatus = "completed";
            claimRequest.PaidAt = DateTime.UtcNow;
            claimRequest.StripePaymentIntentId = sessionDetails.PaymentId;
            claimRequest.UpdatedAt = DateTime.UtcNow;

            Plans.TryGetValue(claimRequest.PlanType, out var plan);

            var listing = new PremiumListing
            {
                VenueId = claimRequest.VenueId,
                PlanType = claimRequest.PlanType,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(claimRequest.DurationMonths),
                MonthlyFee = claimRequest.MonthlyRate,
                IsActive = true,
                FeaturedPlacement = plan?.FeaturedPlacement ?? false,
                PhotoGallery = plan?.PhotoGallery ?? false,
                RoomShowcase = plan?.RoomShowcase ?? false,
                BookingIntegration = plan?.BookingIntegration ?? false,
                AnalyticsDashboard = plan?.AnalyticsDashboard ?? false,
                CreatedAt = DateTime.UtcNow
            };

            _context.PremiumListings.Add(listing);
            await _context.SaveChangesAsync();

            claimRequest.PremiumListingId = listing.ListingId;

            var venue = await _context.Venues.FindAsync(claimRequest.VenueId);
            if (venue != null)
            {
                venue.IsPremium = true;
            }

            var invoiceCount = await _context.Invoices.CountAsync() + 1;
            var invoice = new Models.Domain.Invoice
            {
                InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMM}-{invoiceCount:D4}",
                ClaimRequestId = claimRequest.ClaimRequestId,
                VenueId = claimRequest.VenueId,
                Description = $"{claimRequest.PlanType} Plan - {claimRequest.DurationMonths} month(s)",
                Amount = claimRequest.TotalAmount,
                Status = "paid",
                PaymentMethod = _paymentService.ProviderName.ToLower(),
                PaymentReference = sessionDetails.PaymentId,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment processed via {Provider} for ClaimRequest {ClaimId}. Created Listing {ListingId} and Invoice {InvoiceNumber}",
                _paymentService.ProviderName, claimRequest.ClaimRequestId, listing.ListingId, invoice.InvoiceNumber);
        }

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
                .Include(c => c.Venue)
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

    public class ClaimFormModel
    {
        public int VenueId { get; set; }
        public string PlanType { get; set; } = "Premium";
        public int DurationMonths { get; set; } = 3;
        public string ContactName { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string ContactRole { get; set; } = "Owner";
        public string VerificationMethod { get; set; } = "email";
        public bool AgreeTerms { get; set; }
    }

    public class ListingPlan
    {
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
        public bool FeaturedPlacement { get; set; }
        public bool PhotoGallery { get; set; }
        public bool RoomShowcase { get; set; }
        public bool BookingIntegration { get; set; }
        public bool AnalyticsDashboard { get; set; }
    }

    public class VenueSubmissionModel
    {
        [Required(ErrorMessage = "Venue name is required")]
        [MaxLength(200)]
        public string VenueName { get; set; } = string.Empty;

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

        public string? PlanType { get; set; }

        public bool AgreeTerms { get; set; }
    }
}
