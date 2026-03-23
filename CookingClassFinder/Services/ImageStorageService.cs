namespace CookingClassFinder.Services
{
    public class ImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageStorageService> _logger;

        public ImageStorageService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<ImageStorageService> logger)
        {
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile file, string folder)
        {
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Image saved: {Path}", filePath);
            return $"/uploads/{folder}/{fileName}";
        }

        public Task<bool> DeleteImageAsync(string path)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, path.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Image deleted: {Path}", fullPath);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image: {Path}", path);
                return Task.FromResult(false);
            }
        }

        public string GetImageUrl(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/images/placeholder.jpg";
            if (path.StartsWith("http")) return path;
            return path;
        }
    }
}
