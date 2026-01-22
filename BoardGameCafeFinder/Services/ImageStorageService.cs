using System.Security.Cryptography;
using System.Text;

namespace BoardGameCafeFinder.Services
{
    public interface IImageStorageService
    {
        Task<string?> DownloadAndSaveImageAsync(string imageUrl, string category = "cafes");
        string GetImagePath(string filename);
    }

    public class ImageStorageService : IImageStorageService
    {
        private readonly ILogger<ImageStorageService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _cafeImagesPath;

        public ImageStorageService(
            ILogger<ImageStorageService> logger,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Get cafe images path from config, default to wwwroot/images if not configured
            _cafeImagesPath = _configuration["ImageStorage:CafeImagesPath"] ?? "";
            if (string.IsNullOrEmpty(_cafeImagesPath))
            {
                _cafeImagesPath = Path.Combine(_environment.WebRootPath, "images");
            }
        }

        public async Task<string?> DownloadAndSaveImageAsync(string imageUrl, string category = "cafes")
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl) || !Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
                {
                    _logger.LogWarning("Invalid image URL: {Url}", imageUrl);
                    return null;
                }

                // Generate unique filename based on URL hash
                var filename = GenerateFilename(imageUrl);

                // Use configured path or default to wwwroot/images
                string fullCategoryPath;
                string relativePath;

                if (!string.IsNullOrEmpty(_configuration["ImageStorage:CafeImagesPath"]))
                {
                    // Custom configured path
                    fullCategoryPath = Path.Combine(_cafeImagesPath, category);
                    relativePath = $"/images/{category}/{filename}"; // Still serve from /images URL
                }
                else
                {
                    // Default: wwwroot/images
                    var categoryPath = Path.Combine("images", category);
                    fullCategoryPath = Path.Combine(_environment.WebRootPath, categoryPath);
                    relativePath = $"/{categoryPath.Replace("\\", "/")}/{filename}";
                }

                // Create directory if not exists
                Directory.CreateDirectory(fullCategoryPath);

                var filePath = Path.Combine(fullCategoryPath, filename);

                // Skip if file already exists
                if (File.Exists(filePath))
                {
                    _logger.LogInformation("Image already exists: {Path}", relativePath);
                    return relativePath;
                }

                // Download image with retry logic
                byte[]? imageBytes = null;
                int maxRetries = 3;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger.LogInformation("Downloading image (attempt {Attempt}/{MaxRetries}): {Url}", attempt, maxRetries, imageUrl);

                        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        request.Headers.Add("Accept", "image/webp,image/apng,image/*,*/*;q=0.8");
                        request.Headers.Add("Referer", "https://www.google.com/");

                        using var response = await _httpClient.SendAsync(request);

                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            _logger.LogWarning("Rate limited (429) for {Url}, attempt {Attempt}", imageUrl, attempt);

                            if (attempt < maxRetries)
                            {
                                var delaySeconds = attempt * 2; // Exponential backoff: 2s, 4s, 6s
                                _logger.LogInformation("Waiting {Delay} seconds before retry...", delaySeconds);
                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                                continue;
                            }

                            _logger.LogError("Failed to download image after {MaxRetries} attempts due to rate limiting", maxRetries);
                            return null;
                        }

                        response.EnsureSuccessStatusCode();
                        imageBytes = await response.Content.ReadAsByteArrayAsync();
                        break; // Success, exit retry loop
                    }
                    catch (HttpRequestException ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "HTTP error downloading image (attempt {Attempt}/{MaxRetries}): {Url}", attempt, maxRetries, imageUrl);
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                    }
                }

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    _logger.LogError("Failed to download image after all retries: {Url}", imageUrl);
                    return null;
                }

                // Validate image (basic check)
                if (!IsValidImage(imageBytes))
                {
                    _logger.LogWarning("Downloaded data is not a valid image: {Url}", imageUrl);
                    return null;
                }

                // Save to disk
                await File.WriteAllBytesAsync(filePath, imageBytes);
                _logger.LogInformation("Image saved successfully: {Path} ({Size} bytes)", relativePath, imageBytes.Length);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading and saving image: {Url}", imageUrl);
                return null;
            }
        }

        public string GetImagePath(string filename)
        {
            return Path.Combine(_environment.WebRootPath, "images", filename);
        }

        private string GenerateFilename(string url)
        {
            // Use MD5 hash of URL to generate unique filename
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            // Try to get extension from URL
            var extension = ".jpg"; // Default
            try
            {
                var uri = new Uri(url);
                var lastSegment = uri.Segments.LastOrDefault() ?? "";
                if (lastSegment.Contains('.'))
                {
                    var ext = Path.GetExtension(lastSegment).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(ext) && (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".gif"))
                    {
                        extension = ext;
                    }
                }
            }
            catch
            {
                // Use default extension
            }

            return $"{hashString}{extension}";
        }

        private bool IsValidImage(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length < 12)
                return false;

            // Check for common image file signatures
            // JPEG: FF D8 FF
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return true;

            // GIF: 47 49 46
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                return true;

            // WebP: RIFF....WEBP
            if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
                imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
                return true;

            return false;
        }
    }
}
