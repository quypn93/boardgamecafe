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

        public AdminController(IBggSyncService bggSyncService, ApplicationDbContext context)
        {
            _bggSyncService = bggSyncService;
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Get statistics
            var totalCafes = await _context.Cafes.CountAsync();
            var totalGames = await _context.BoardGames.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalReviews = await _context.Reviews.CountAsync();

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
            ViewBag.RecentCafes = recentCafes;
            ViewBag.TopCities = topCities;

            return View();
        }

        public async Task<IActionResult> Index()
        {
            var cafes = await _context.Cafes
                .Include(c => c.CafeGames)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var cities = cafes.Select(c => c.City).Distinct().OrderBy(c => c).ToList();
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
    }
}
