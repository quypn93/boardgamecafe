using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using EscapeRoomFinder.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoomFinder.Controllers
{
    public class VenueController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IVenueService _venueService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<VenueController> _logger;

        public VenueController(
            ApplicationDbContext context,
            IVenueService venueService,
            UserManager<User> userManager,
            ILogger<VenueController> logger)
        {
            _context = context;
            _venueService = venueService;
            _userManager = userManager;
            _logger = logger;
        }

        [Route("venue/{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            var venue = await _venueService.GetBySlugAsync(slug);

            if (venue == null)
            {
                return NotFound();
            }

            // Get nearby venues
            ViewBag.NearbyVenues = await _venueService.GetNearbyVenuesAsync(
                venue.Latitude, venue.Longitude, 30, 5);

            return View(venue);
        }

        [Route("venue/{slug}/room/{roomSlug}")]
        public async Task<IActionResult> RoomDetails(string slug, string roomSlug)
        {
            var venue = await _venueService.GetBySlugAsync(slug);
            if (venue == null)
            {
                return NotFound();
            }

            var room = await _context.Rooms
                .Include(r => r.Reviews.Where(rv => rv.IsApproved))
                    .ThenInclude(rv => rv.User)
                .Include(r => r.Photos.Where(p => p.IsApproved))
                .FirstOrDefaultAsync(r => r.Slug == roomSlug && r.VenueId == venue.VenueId);

            if (room == null)
            {
                return NotFound();
            }

            ViewBag.Venue = venue;
            ViewBag.OtherRooms = venue.Rooms.Where(r => r.RoomId != room.RoomId && r.IsActive).Take(4);

            return View(room);
        }

        [Authorize]
        [HttpPost]
        [Route("venue/{venueId}/review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int venueId, int rating, string? title, string? content)
        {
            var venue = await _context.Venues.FindAsync(venueId);
            if (venue == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Check if user already reviewed this venue
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.VenueId == venueId && r.UserId == user.Id);

            if (existingReview != null)
            {
                TempData["Error"] = "You have already reviewed this venue.";
                return RedirectToAction(nameof(Details), new { slug = venue.Slug });
            }

            var review = new Review
            {
                VenueId = venueId,
                UserId = user.Id,
                Rating = rating,
                Title = title,
                Content = content,
                VisitDate = DateTime.UtcNow,
                IsApproved = false, // Requires admin approval
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            // Update user stats
            user.TotalReviews++;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Thank you for your review! It will be visible after approval.";
            return RedirectToAction(nameof(Details), new { slug = venue.Slug });
        }

        [Authorize]
        [HttpPost]
        [Route("venue/{venueId}/room/{roomId}/review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRoomReview(
            int venueId,
            int roomId,
            int rating,
            int? puzzleQualityRating,
            int? themeImmersionRating,
            int? staffRating,
            int? valueForMoneyRating,
            string? title,
            string? content,
            bool? didEscape,
            int? timeRemainingSeconds,
            int? hintsUsed,
            int? teamSize,
            int? perceivedDifficulty,
            bool containsSpoilers)
        {
            var room = await _context.Rooms.Include(r => r.Venue).FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (room == null || room.VenueId != venueId)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Check if user already reviewed this room
            var existingReview = await _context.RoomReviews
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.UserId == user.Id);

            if (existingReview != null)
            {
                TempData["Error"] = "You have already reviewed this room.";
                return RedirectToAction(nameof(RoomDetails), new { slug = room.Venue.Slug, roomSlug = room.Slug });
            }

            var review = new RoomReview
            {
                RoomId = roomId,
                UserId = user.Id,
                Rating = rating,
                PuzzleQualityRating = puzzleQualityRating,
                ThemeImmersionRating = themeImmersionRating,
                StaffRating = staffRating,
                ValueForMoneyRating = valueForMoneyRating,
                Title = title,
                Content = content,
                DidEscape = didEscape,
                TimeRemainingSeconds = timeRemainingSeconds,
                HintsUsed = hintsUsed,
                TeamSize = teamSize,
                PerceivedDifficulty = perceivedDifficulty,
                ContainsSpoilers = containsSpoilers,
                PlayDate = DateTime.UtcNow,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RoomReviews.Add(review);
            await _context.SaveChangesAsync();

            // Update user stats
            user.TotalReviews++;
            user.TotalRoomsPlayed++;
            if (didEscape == true)
            {
                user.TotalEscapes++;
            }
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Thank you for your review! It will be visible after approval.";
            return RedirectToAction(nameof(RoomDetails), new { slug = room.Venue.Slug, roomSlug = room.Slug });
        }

        [Route("cities")]
        public async Task<IActionResult> Cities()
        {
            var cities = await _context.Venues
                .Where(v => v.IsActive)
                .GroupBy(v => new { v.City, v.Country })
                .Select(g => new
                {
                    g.Key.City,
                    g.Key.Country,
                    VenueCount = g.Count(),
                    RoomCount = g.Sum(v => v.TotalRooms)
                })
                .OrderByDescending(c => c.VenueCount)
                .ToListAsync();

            return View(cities);
        }

        [Route("themes")]
        public async Task<IActionResult> Themes()
        {
            var themes = await _context.Rooms
                .Where(r => r.IsActive)
                .GroupBy(r => r.Theme)
                .Select(g => new
                {
                    Theme = g.Key,
                    RoomCount = g.Count(),
                    AvgDifficulty = g.Average(r => r.Difficulty),
                    AvgRating = g.Average(r => r.AverageRating ?? 0)
                })
                .OrderByDescending(t => t.RoomCount)
                .ToListAsync();

            return View(themes);
        }
    }
}
