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
    }
}
