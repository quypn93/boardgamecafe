using Microsoft.AspNetCore.Mvc;

namespace BoardGameCafeFinder.Controllers
{
    [Route("")]
    [ApiController]
    public class FaviconController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FaviconController> _logger;

        public FaviconController(IWebHostEnvironment environment, ILogger<FaviconController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Serve favicon.ico
        /// GET: /favicon.ico
        /// </summary>
        [HttpGet("favicon.ico")]
        [Produces("image/x-icon")]
        public async Task<IActionResult> Favicon()
        {
            try
            {
                var faviconPath = Path.Combine(_environment.WebRootPath, "favicon.ico");
                if (System.IO.File.Exists(faviconPath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(faviconPath);
                    return File(fileBytes, "image/x-icon");
                }

                // Fallback: return a simple 1x1 transparent PNG if favicon.ico doesn't exist
                var emptyPng = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 10, 73, 68, 65, 84, 8, 99, 63, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                return File(emptyPng, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving favicon");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Serve apple touch icon
        /// GET: /apple-touch-icon.png
        /// </summary>
        [HttpGet("apple-touch-icon.png")]
        [Produces("image/png")]
        public async Task<IActionResult> AppleTouchIcon()
        {
            try
            {
                var iconPath = Path.Combine(_environment.WebRootPath, "apple-touch-icon.png");
                if (System.IO.File.Exists(iconPath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(iconPath);
                    return File(fileBytes, "image/png");
                }

                // Return a simple 180x180 PNG if file doesn't exist
                var blankPng = GenerateBlankPNG(180, 180);
                return File(blankPng, "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving apple touch icon");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Generate a simple blank PNG (fallback)
        /// </summary>
        private byte[] GenerateBlankPNG(int width, int height)
        {
            // Simple 1x1 transparent PNG as fallback
            return new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 10, 73, 68, 65, 84, 8, 99, 63, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
        }
    }
}
