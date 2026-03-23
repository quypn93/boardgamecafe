using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;

namespace CookingClassFinder.Controllers;

public class ListingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPaymentService _paymentService;

    public ListingController(ApplicationDbContext context, IPaymentService paymentService)
    {
        _context = context;
        _paymentService = paymentService;
    }

    public IActionResult SubmitSchool()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SubmitSchool(CookingSchool model)
    {
        if (ModelState.IsValid)
        {
            model.Slug = GenerateSlug(model.Name);
            model.IsActive = false; // Pending approval
            model.IsVerified = false;
            model.CreatedAt = DateTime.UtcNow;

            _context.Schools.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your school has been submitted for review. We'll notify you once it's approved.";
            return RedirectToAction("Index", "Home");
        }

        return View(model);
    }

    public IActionResult Claim()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SubmitClaim(ClaimRequest model)
    {
        if (ModelState.IsValid)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                model.UserId = userId;
            }
            model.PaymentStatus = "pending";
            model.CreatedAt = DateTime.UtcNow;

            _context.ClaimRequests.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your claim request has been submitted. We'll contact you within 2-3 business days.";
            return RedirectToAction("Index", "Home");
        }

        return View("Claim", model);
    }

    public IActionResult Pricing()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Subscribe(string plan)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            return Unauthorized();

        decimal amount = plan switch
        {
            "professional" => 29.00m,
            "enterprise" => 79.00m,
            _ => 0
        };

        if (amount == 0)
            return BadRequest("Invalid plan");

        // Create a claim request for the subscription
        var claimRequest = new ClaimRequest
        {
            UserId = userId,
            PlanType = plan,
            TotalAmount = amount,
            PaymentStatus = "pending",
            ContactName = "Subscriber",
            ContactEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "",
            CreatedAt = DateTime.UtcNow
        };

        var sessionUrl = await _paymentService.CreateCheckoutSessionAsync(
            claimRequest,
            $"/Listing/SubscribeSuccess?plan={plan}",
            "/Listing/Pricing"
        );

        return Redirect(sessionUrl);
    }

    public IActionResult SubscribeSuccess(string plan)
    {
        TempData["Success"] = $"Thank you for subscribing to the {plan} plan!";
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Invoice(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var invoice = await _context.Invoices
            .Include(i => i.User)
            .Include(i => i.PremiumListing)
                .ThenInclude(p => p!.School)
            .FirstOrDefaultAsync(i => i.InvoiceId == id && i.UserId.ToString() == userId);

        if (invoice == null)
            return NotFound();

        return View(invoice);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "");

        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

        return slug.Trim('-');
    }
}
