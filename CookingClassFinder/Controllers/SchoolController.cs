using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using CookingClassFinder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookingClassFinder.Controllers
{
    public class SchoolController : Controller
    {
        private readonly ISchoolService _schoolService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<SchoolController> _logger;

        public SchoolController(
            ISchoolService schoolService,
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<SchoolController> logger)
        {
            _schoolService = schoolService;
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [Route("school/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            var school = await _schoolService.GetBySlugAsync(slug);
            if (school == null)
                return NotFound();

            return View(school);
        }

        [Route("class/{schoolSlug}/{classSlug}")]
        public async Task<IActionResult> ClassDetails(string schoolSlug, string classSlug)
        {
            var cookingClass = await _context.Classes
                .Include(c => c.School)
                .Include(c => c.Reviews.Where(r => r.IsApproved))
                .Include(c => c.Photos.Where(p => p.IsApproved))
                .FirstOrDefaultAsync(c => c.Slug == classSlug && c.School.Slug == schoolSlug && c.IsActive);

            if (cookingClass == null)
                return NotFound();

            return View(cookingClass);
        }

        [HttpPost]
        [Authorize]
        [Route("school/{slug}/review")]
        public async Task<IActionResult> AddReview(string slug, int rating, string? title, string? content)
        {
            var school = await _schoolService.GetBySlugAsync(slug);
            if (school == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            var review = new Review
            {
                SchoolId = school.SchoolId,
                UserId = user.Id,
                Rating = rating,
                Title = title,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsApproved = false // Requires admin approval
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your review has been submitted and is pending approval.";
            return RedirectToAction("Details", new { slug });
        }

        [Route("cuisines")]
        public async Task<IActionResult> Cuisines()
        {
            var cuisines = await _schoolService.GetAllCuisineTypesAsync();
            return View(cuisines);
        }

        [Route("cuisines/{cuisine}")]
        public async Task<IActionResult> ByCuisine(string cuisine)
        {
            var schools = await _schoolService.FilterSchoolsAsync(null, null, cuisine, null, null, null);
            ViewBag.Cuisine = cuisine;
            return View(schools);
        }
    }
}
