using VRArcadeFinder.Data;
using VRArcadeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VRArcadeFinder.Services
{
    public interface IBlogService
    {
        // CRUD Operations
        Task<List<BlogPost>> GetAllPostsAsync(bool includeUnpublished = false);
        Task<BlogPost?> GetPostByIdAsync(int id);
        Task<BlogPost?> GetPostBySlugAsync(string slug);
        Task<BlogPost> CreatePostAsync(BlogPost post);
        Task<BlogPost> UpdatePostAsync(BlogPost post);
        Task<bool> DeletePostAsync(int id);

        // Query Operations
        Task<List<BlogPost>> GetPublishedPostsAsync(int page = 1, int pageSize = 10);
        Task<List<BlogPost>> GetPostsByCategoryAsync(string category, int page = 1, int pageSize = 10);
        Task<List<BlogPost>> GetPostsByCityAsync(string city, int page = 1, int pageSize = 10);
        Task<int> GetTotalPublishedCountAsync();
        Task<List<string>> GetAllCategoriesAsync();
        Task<List<string>> GetAllCitiesWithPostsAsync();

        // Auto-Generation
        Task<BlogPost> GenerateTopGamesPostAsync(string city, string? country = null, string? culture = null);
        Task<List<BlogPost>> GenerateTopGamesPostsForCitiesAsync(List<string> cities);
        Task<BlogPost> GenerateCityGuidePostAsync(string city, string? country = null, string? culture = null);

        // Utilities
        Task IncrementViewCountAsync(int id);
        string GenerateSlug(string title);
    }

    public class BlogService : IBlogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BlogService> _logger;

        public BlogService(
            ApplicationDbContext context,
            ILogger<BlogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region CRUD Operations

        public async Task<List<BlogPost>> GetAllPostsAsync(bool includeUnpublished = false)
        {
            var query = _context.BlogPosts.AsQueryable();

            if (!includeUnpublished)
            {
                query = query.Where(p => p.IsPublished);
            }

            return await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        }

        public async Task<BlogPost?> GetPostByIdAsync(int id)
        {
            return await _context.BlogPosts.FindAsync(id);
        }

        public async Task<BlogPost?> GetPostBySlugAsync(string slug)
        {
            return await _context.BlogPosts.FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task<BlogPost> CreatePostAsync(BlogPost post)
        {
            post.CreatedAt = DateTime.UtcNow;
            post.Slug = await EnsureUniqueSlugAsync(GenerateSlug(post.Title));

            if (post.IsPublished && !post.PublishedAt.HasValue)
            {
                post.PublishedAt = DateTime.UtcNow;
            }

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created blog post: {Title}", post.Title);
            return post;
        }

        public async Task<BlogPost> UpdatePostAsync(BlogPost post)
        {
            post.UpdatedAt = DateTime.UtcNow;

            if (post.IsPublished && !post.PublishedAt.HasValue)
            {
                post.PublishedAt = DateTime.UtcNow;
            }

            _context.BlogPosts.Update(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated blog post: {Title}", post.Title);
            return post;
        }

        public async Task<bool> DeletePostAsync(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return false;

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted blog post: {Title}", post.Title);
            return true;
        }

        #endregion

        #region Query Operations

        public async Task<List<BlogPost>> GetPublishedPostsAsync(int page = 1, int pageSize = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<BlogPost>> GetPostsByCategoryAsync(string category, int page = 1, int pageSize = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.Category == category)
                .OrderByDescending(p => p.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<BlogPost>> GetPostsByCityAsync(string city, int page = 1, int pageSize = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.RelatedCity == city)
                .OrderByDescending(p => p.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalPublishedCountAsync()
        {
            return await _context.BlogPosts.CountAsync(p => p.IsPublished);
        }

        public async Task<List<string>> GetAllCategoriesAsync()
        {
            return await _context.BlogPosts
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetAllCitiesWithPostsAsync()
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && !string.IsNullOrEmpty(p.RelatedCity))
                .Select(p => p.RelatedCity!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        #endregion

        #region Auto-Generation

        public async Task<BlogPost> GenerateTopGamesPostAsync(string city, string? country = null, string? culture = null)
        {
            _logger.LogInformation("Generating top VR games post for city: {City} in culture: {Culture}", city, culture ?? "en");

            // Get arcades in this city
            var arcadesQuery = _context.Arcades
                .Where(c => c.IsActive && c.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(country))
            {
                arcadesQuery = arcadesQuery.Where(c => c.Country.ToLower() == country.ToLower());
            }

            var arcadeIds = await arcadesQuery.Select(c => c.ArcadeId).ToListAsync();

            if (!arcadeIds.Any())
            {
                throw new InvalidOperationException($"No arcades found in {city}");
            }

            // Get top games from these arcades
            var topGames = await _context.ArcadeGames
                .Where(cg => arcadeIds.Contains(cg.ArcadeId))
                .GroupBy(cg => cg.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    ArcadeCount = g.Count()
                })
                .OrderByDescending(g => g.ArcadeCount)
                .Take(20)
                .ToListAsync();

            var gameIds = topGames.Select(g => g.GameId).ToList();
            var games = await _context.VRGames
                .Where(g => gameIds.Contains(g.GameId))
                .ToListAsync();

            // Generate content
            var content = GenerateTopGamesContent(city, games, topGames, arcadeIds.Count);
            var title = $"Top VR Games in {city}";
            var summary = $"Discover the most popular VR games available at arcades in {city}";

            var post = new BlogPost
            {
                Title = title,
                Slug = await EnsureUniqueSlugAsync(GenerateSlug(title)),
                Summary = summary,
                Content = content,
                Category = "Top Games",
                Tags = $"{city},VR games,top games,arcade",
                MetaTitle = $"{title} | VR Arcade Finder",
                MetaDescription = summary,
                Author = "VR Arcade Finder",
                IsPublished = false,
                IsAutoGenerated = true,
                RelatedCity = city,
                RelatedCountry = country,
                PostType = "top-games",
                CreatedAt = DateTime.UtcNow
            };

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated top VR games post for {City}: {Title}", city, title);
            return post;
        }

        public async Task<List<BlogPost>> GenerateTopGamesPostsForCitiesAsync(List<string> cities)
        {
            var posts = new List<BlogPost>();

            foreach (var city in cities)
            {
                try
                {
                    var existingPost = await _context.BlogPosts
                        .FirstOrDefaultAsync(p => p.RelatedCity == city && p.PostType == "top-games");

                    if (existingPost != null)
                    {
                        _logger.LogInformation("Top games post already exists for {City}, skipping", city);
                        continue;
                    }

                    var post = await GenerateTopGamesPostAsync(city);
                    posts.Add(post);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate post for city: {City}", city);
                }
            }

            return posts;
        }

        public async Task<BlogPost> GenerateCityGuidePostAsync(string city, string? country = null, string? culture = null)
        {
            _logger.LogInformation("Generating city guide post for: {City}", city);

            var arcadesQuery = _context.Arcades
                .Include(c => c.ArcadeGames)
                .Include(c => c.Photos)
                .Where(c => c.IsActive && c.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(country))
            {
                arcadesQuery = arcadesQuery.Where(c => c.Country.ToLower() == country.ToLower());
            }

            var arcades = await arcadesQuery.OrderByDescending(c => c.AverageRating).ToListAsync();

            if (!arcades.Any())
            {
                throw new InvalidOperationException($"No arcades found in {city}");
            }

            var content = GenerateCityGuideContent(city, arcades);
            var title = $"VR Arcade Guide: {city}";
            var summary = $"Your complete guide to VR arcades in {city}";

            var post = new BlogPost
            {
                Title = title,
                Slug = await EnsureUniqueSlugAsync(GenerateSlug(title)),
                Summary = summary,
                Content = content,
                Category = "City Guide",
                Tags = $"{city},VR arcade,guide,where to play",
                MetaTitle = $"{title} | VR Arcade Finder",
                MetaDescription = $"Discover {arcades.Count} VR arcades in {city}. {summary}",
                Author = "VR Arcade Finder",
                IsPublished = false,
                IsAutoGenerated = true,
                RelatedCity = city,
                RelatedCountry = country,
                PostType = "city-guide",
                CreatedAt = DateTime.UtcNow
            };

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated city guide post for {City}: {Title}", city, title);
            return post;
        }

        private string GenerateTopGamesContent(string city, List<VRGame> games, dynamic topGames, int arcadeCount)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"<p class=\"lead\">Discover the most popular VR games available at {arcadeCount} arcades in {city}!</p>");

            sb.AppendLine("<h2>Why These Games?</h2>");
            sb.AppendLine($"<p>We've analyzed the game libraries at VR arcades across {city} to bring you the most commonly available and beloved titles.</p>");

            sb.AppendLine($"<h2>Top VR Games in {city}</h2>");

            int rank = 1;
            foreach (var topGame in (IEnumerable<dynamic>)topGames)
            {
                var game = games.FirstOrDefault(g => g.GameId == topGame.GameId);
                if (game == null) continue;

                sb.AppendLine("<div class=\"game-card mb-4 p-3 border rounded\">");
                sb.AppendLine("<div class=\"row\">");

                if (!string.IsNullOrEmpty(game.ImageUrl))
                {
                    sb.AppendLine("<div class=\"col-md-3 col-sm-4 mb-3 mb-md-0 text-center\">");
                    sb.AppendLine($"<img src=\"{game.ImageUrl}\" alt=\"{game.Name}\" class=\"img-fluid rounded\" style=\"max-height: 150px; object-fit: contain;\">");
                    sb.AppendLine("</div>");
                    sb.AppendLine("<div class=\"col-md-9 col-sm-8\">");
                }
                else
                {
                    sb.AppendLine("<div class=\"col-12\">");
                }

                sb.AppendLine($"<h3>{rank}. {game.Name}</h3>");

                if (!string.IsNullOrEmpty(game.Description))
                {
                    var shortDesc = game.Description.Length > 300
                        ? game.Description.Substring(0, 297) + "..."
                        : game.Description;
                    sb.AppendLine($"<p>{shortDesc}</p>");
                }

                sb.AppendLine("<div class=\"game-details\">");
                if (game.MinPlayers.HasValue && game.MaxPlayers.HasValue)
                {
                    sb.AppendLine($"<span class=\"badge bg-primary me-2\">{game.MinPlayers}-{game.MaxPlayers} players</span>");
                }
                if (!string.IsNullOrEmpty(game.Genre))
                {
                    sb.AppendLine($"<span class=\"badge bg-info me-2\">{game.Genre}</span>");
                }
                if (!string.IsNullOrEmpty(game.VRPlatform))
                {
                    sb.AppendLine($"<span class=\"badge bg-secondary me-2\">{game.VRPlatform}</span>");
                }
                if (!string.IsNullOrEmpty(game.IntensityLevel))
                {
                    sb.AppendLine($"<span class=\"badge {game.GetIntensityBadgeClass()}\">{game.IntensityLevel} intensity</span>");
                }
                sb.AppendLine("</div>");

                sb.AppendLine($"<p class=\"text-muted mt-2\"><small>Available at {topGame.ArcadeCount} arcades in {city}</small></p>");

                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
                sb.AppendLine("</div>");

                rank++;
            }

            sb.AppendLine("<h2>Where to Play</h2>");
            sb.AppendLine($"<p>Ready to experience these amazing VR games? <a href=\"/?city={Uri.EscapeDataString(city)}\">Browse all VR arcades in {city}</a></p>");

            return sb.ToString();
        }

        private string GenerateCityGuideContent(string city, List<Arcade> arcades)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"<p class=\"lead\">Your complete guide to experiencing virtual reality in {city}!</p>");

            sb.AppendLine("<h2>Overview</h2>");
            sb.AppendLine($"<p>{city} has {arcades.Count} VR arcades offering immersive virtual reality experiences. Whether you're a VR enthusiast or trying it for the first time, there's something for everyone.</p>");

            sb.AppendLine("<h2>Top Rated VR Arcades</h2>");

            foreach (var arcade in arcades.Take(5))
            {
                sb.AppendLine("<div class=\"arcade-card mb-4 p-3 border rounded\">");
                sb.AppendLine("<div class=\"row\">");

                var photo = arcade.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault();
                var imageUrl = photo?.Url ?? photo?.LocalPath ?? arcade.LocalImagePath;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    sb.AppendLine("<div class=\"col-md-4 mb-3 mb-md-0\">");
                    sb.AppendLine($"<a href=\"/arcade/{arcade.Slug}\">");
                    sb.AppendLine($"<img src=\"{imageUrl}\" alt=\"{arcade.Name}\" class=\"img-fluid rounded\" style=\"width: 100%; height: 200px; object-fit: cover;\">");
                    sb.AppendLine("</a>");
                    sb.AppendLine("</div>");
                    sb.AppendLine("<div class=\"col-md-8\">");
                }
                else
                {
                    sb.AppendLine("<div class=\"col-12\">");
                }

                sb.AppendLine($"<h3><a href=\"/arcade/{arcade.Slug}\">{arcade.Name}</a></h3>");

                if (arcade.AverageRating.HasValue)
                {
                    sb.AppendLine($"<p><strong>Rating:</strong> {arcade.AverageRating:F1}/5</p>");
                }

                if (!string.IsNullOrEmpty(arcade.Address))
                {
                    sb.AppendLine($"<p><strong>Address:</strong> {arcade.Address}</p>");
                }

                if (!string.IsNullOrEmpty(arcade.VRPlatforms))
                {
                    sb.AppendLine($"<p><strong>VR Platforms:</strong> {arcade.VRPlatforms}</p>");
                }

                var gameCount = arcade.ArcadeGames?.Count ?? 0;
                if (gameCount > 0)
                {
                    sb.AppendLine($"<p><strong>Games:</strong> {gameCount}+ VR experiences available</p>");
                }

                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("<h2>Tips for First-Time VR Visitors</h2>");
            sb.AppendLine("<ul>");
            sb.AppendLine("<li>Book ahead during weekends - VR arcades can get busy!</li>");
            sb.AppendLine("<li>Wear comfortable clothes that allow movement</li>");
            sb.AppendLine("<li>If you're prone to motion sickness, start with seated experiences</li>");
            sb.AppendLine("<li>Ask staff for game recommendations based on your experience level</li>");
            sb.AppendLine("</ul>");

            sb.AppendLine("<h2>Explore More</h2>");
            sb.AppendLine($"<p><a href=\"/?city={Uri.EscapeDataString(city)}\" class=\"btn btn-primary\">View All VR Arcades in {city}</a></p>");

            return sb.ToString();
        }

        #endregion

        #region Utilities

        public async Task IncrementViewCountAsync(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post != null)
            {
                post.ViewCount++;
                await _context.SaveChangesAsync();
            }
        }

        public string GenerateSlug(string title)
        {
            var slug = title.ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            if (slug.Length > 100)
            {
                slug = slug.Substring(0, 100).TrimEnd('-');
            }

            return slug;
        }

        private async Task<string> EnsureUniqueSlugAsync(string baseSlug)
        {
            var slug = baseSlug;
            var counter = 1;

            while (await _context.BlogPosts.AnyAsync(p => p.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }

        #endregion
    }
}
