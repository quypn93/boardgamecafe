using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Services
{
    public class ImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageStorageService> _logger;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        public ImageStorageService(IWebHostEnvironment environment, ILogger<ImageStorageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Invalid file");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!_allowedExtensions.Contains(extension))
                throw new ArgumentException($"File type {extension} is not allowed");

            var fileName = $"{Guid.NewGuid()}{extension}";
            var relativePath = Path.Combine("images", folder, fileName);
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("Saved image to {Path}", relativePath);

            return "/" + relativePath.Replace("\\", "/");
        }

        public async Task<string> SaveImageFromUrlAsync(string imageUrl, string folder, string fileName)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var response = await httpClient.GetAsync(imageUrl);
                response.EnsureSuccessStatusCode();

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/gif" => ".gif",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };

                var safeFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9-]", "-").ToLower();
                var finalFileName = $"{safeFileName}-{DateTime.UtcNow.Ticks}{extension}";
                var relativePath = Path.Combine("images", folder, finalFileName);
                var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                var imageBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(fullPath, imageBytes);

                _logger.LogInformation("Downloaded and saved image from {Url} to {Path}", imageUrl, relativePath);

                return "/" + relativePath.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download image from {Url}", imageUrl);
                throw;
            }
        }

        public Task<bool> DeleteImageAsync(string relativePath)
        {
            try
            {
                if (string.IsNullOrEmpty(relativePath))
                    return Task.FromResult(false);

                // Remove leading slash if present
                relativePath = relativePath.TrimStart('/');
                var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted image at {Path}", relativePath);
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image at {Path}", relativePath);
                return Task.FromResult(false);
            }
        }

        public string GetImageUrl(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return "/images/placeholder-venue.jpg";

            return relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
        }
    }
}
