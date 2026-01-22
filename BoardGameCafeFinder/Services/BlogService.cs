using BoardGameCafeFinder.Data;
using BoardGameCafeFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace BoardGameCafeFinder.Services
{
    public class BlogService : IBlogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BlogService> _logger;

        public BlogService(ApplicationDbContext context, ILogger<BlogService> logger)
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

        public async Task<BlogPost> GenerateTopGamesPostAsync(string city, string? country = null)
        {
            _logger.LogInformation("Generating top games post for city: {City}", city);

            // Get cafes in this city
            var cafesQuery = _context.Cafes
                .Where(c => c.IsActive && c.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(country))
            {
                cafesQuery = cafesQuery.Where(c => c.Country.ToLower() == country.ToLower());
            }

            var cafeIds = await cafesQuery.Select(c => c.CafeId).ToListAsync();

            if (!cafeIds.Any())
            {
                throw new InvalidOperationException($"No cafes found in {city}");
            }

            // Get top games from these cafes
            var topGames = await _context.CafeGames
                .Where(cg => cafeIds.Contains(cg.CafeId))
                .GroupBy(cg => cg.GameId)
                .Select(g => new
                {
                    GameId = g.Key,
                    CafeCount = g.Count()
                })
                .OrderByDescending(g => g.CafeCount)
                .Take(20)
                .ToListAsync();

            var gameIds = topGames.Select(g => g.GameId).ToList();
            var games = await _context.BoardGames
                .Where(g => gameIds.Contains(g.GameId))
                .ToListAsync();

            // Generate content
            var content = GenerateTopGamesContent(city, games, topGames, cafeIds.Count);
            var title = $"Top Board Games at Cafes in {city}";

            var post = new BlogPost
            {
                Title = title,
                Slug = await EnsureUniqueSlugAsync(GenerateSlug(title)),
                Summary = $"Discover the most popular board games available at board game cafes in {city}. From family favorites to strategic masterpieces.",
                Content = content,
                Category = "Top Games",
                Tags = $"{city},board games,top games,cafe",
                MetaTitle = $"Top Board Games at Cafes in {city} | Board Game Cafe Finder",
                MetaDescription = $"Explore the most popular board games at {cafeIds.Count} board game cafes in {city}. Find where to play your favorite games.",
                Author = "Board Game Cafe Finder",
                IsPublished = false,
                IsAutoGenerated = true,
                RelatedCity = city,
                RelatedCountry = country,
                PostType = "top-games",
                CreatedAt = DateTime.UtcNow
            };

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated top games post for {City}: {Title}", city, title);
            return post;
        }

        public async Task<List<BlogPost>> GenerateTopGamesPostsForCitiesAsync(List<string> cities)
        {
            var posts = new List<BlogPost>();

            foreach (var city in cities)
            {
                try
                {
                    // Check if post already exists for this city
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

        public async Task<BlogPost> GenerateCityGuidePostAsync(string city, string? country = null)
        {
            _logger.LogInformation("Generating city guide post for: {City}", city);

            // Get cafes in this city
            var cafesQuery = _context.Cafes
                .Include(c => c.CafeGames)
                .Include(c => c.Photos)
                .Where(c => c.IsActive && c.City.ToLower() == city.ToLower());

            if (!string.IsNullOrEmpty(country))
            {
                cafesQuery = cafesQuery.Where(c => c.Country.ToLower() == country.ToLower());
            }

            var cafes = await cafesQuery.OrderByDescending(c => c.AverageRating).ToListAsync();

            if (!cafes.Any())
            {
                throw new InvalidOperationException($"No cafes found in {city}");
            }

            // Generate content
            var content = GenerateCityGuideContent(city, cafes);
            var title = $"Board Game Cafes in {city} - Complete Guide";

            var post = new BlogPost
            {
                Title = title,
                Slug = await EnsureUniqueSlugAsync(GenerateSlug(title)),
                Summary = $"Your complete guide to board game cafes in {city}. Find the best places to play, what games they have, and more.",
                Content = content,
                Category = "City Guide",
                Tags = $"{city},board game cafe,guide,where to play",
                MetaTitle = $"Board Game Cafes in {city} - Complete Guide | Board Game Cafe Finder",
                MetaDescription = $"Discover {cafes.Count} board game cafes in {city}. Read our complete guide with reviews, game libraries, and tips.",
                Author = "Board Game Cafe Finder",
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

        private string GenerateTopGamesContent(string city, List<BoardGame> games, dynamic topGames, int cafeCount)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"<p class=\"lead\">Looking for the best board games to play in {city}? We've analyzed data from <strong>{cafeCount} board game cafes</strong> in the area to bring you the most popular games that locals love.</p>");

            sb.AppendLine("<h2>Why These Games?</h2>");
            sb.AppendLine($"<p>These games appear most frequently across board game cafes in {city}, meaning you're likely to find them wherever you go. They're crowd favorites that offer something for everyone.</p>");

            sb.AppendLine("<h2>Top Board Games in " + city + "</h2>");

            int rank = 1;
            foreach (var topGame in (IEnumerable<dynamic>)topGames)
            {
                var game = games.FirstOrDefault(g => g.GameId == topGame.GameId);
                if (game == null) continue;

                sb.AppendLine($"<div class=\"game-card mb-4 p-3 border rounded\">");
                sb.AppendLine("<div class=\"row\">");

                // Show game image if available
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
                    sb.AppendLine($"<span class=\"badge bg-primary me-2\"><i class=\"bi bi-people\"></i> {game.MinPlayers}-{game.MaxPlayers} Players</span>");
                }
                if (game.PlaytimeMinutes.HasValue)
                {
                    sb.AppendLine($"<span class=\"badge bg-secondary me-2\"><i class=\"bi bi-clock\"></i> {game.PlaytimeMinutes} min</span>");
                }
                if (!string.IsNullOrEmpty(game.Category))
                {
                    sb.AppendLine($"<span class=\"badge bg-info\"><i class=\"bi bi-tag\"></i> {game.Category}</span>");
                }
                sb.AppendLine("</div>");

                sb.AppendLine($"<p class=\"text-muted mt-2\"><small>Available at {topGame.CafeCount} cafes in {city}</small></p>");

                sb.AppendLine("</div>"); // Close content column
                sb.AppendLine("</div>"); // Close row
                sb.AppendLine("</div>"); // Close game-card

                rank++;
            }

            sb.AppendLine("<h2>Where to Play</h2>");
            sb.AppendLine($"<p>Ready to try these games? <a href=\"/?city={Uri.EscapeDataString(city)}\">Browse all board game cafes in {city}</a> to find the perfect spot for your next game night.</p>");

            return sb.ToString();
        }

        private string GenerateCityGuideContent(string city, List<Cafe> cafes)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"<p class=\"lead\">Welcome to our comprehensive guide to board game cafes in {city}! Whether you're a seasoned gamer or just looking for a fun night out, we've got you covered.</p>");

            sb.AppendLine("<h2>Overview</h2>");
            sb.AppendLine($"<p>{city} is home to <strong>{cafes.Count} board game cafes</strong>, offering thousands of games across various genres. From cozy spots perfect for date nights to spacious venues ideal for larger groups, there's something for everyone.</p>");

            sb.AppendLine("<h2>Top Rated Cafes</h2>");

            foreach (var cafe in cafes.Take(5))
            {
                sb.AppendLine("<div class=\"cafe-card mb-4 p-3 border rounded\">");
                sb.AppendLine("<div class=\"row\">");

                // Get first photo for this cafe
                var photo = cafe.Photos?.OrderBy(p => p.DisplayOrder).FirstOrDefault();
                var imageUrl = photo?.Url ?? photo?.LocalPath ?? cafe.LocalImagePath;

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    sb.AppendLine("<div class=\"col-md-4 mb-3 mb-md-0\">");
                    sb.AppendLine($"<a href=\"/cafe/{cafe.Slug}\">");
                    sb.AppendLine($"<img src=\"{imageUrl}\" alt=\"{cafe.Name}\" class=\"img-fluid rounded\" style=\"width: 100%; height: 200px; object-fit: cover;\">");
                    sb.AppendLine("</a>");
                    sb.AppendLine("</div>");
                    sb.AppendLine("<div class=\"col-md-8\">");
                }
                else
                {
                    sb.AppendLine("<div class=\"col-12\">");
                }

                sb.AppendLine($"<h3><a href=\"/cafe/{cafe.Slug}\">{cafe.Name}</a></h3>");

                if (cafe.AverageRating.HasValue)
                {
                    sb.AppendLine($"<p><strong>Rating:</strong> {cafe.AverageRating:F1}/5 ⭐</p>");
                }

                if (!string.IsNullOrEmpty(cafe.Address))
                {
                    sb.AppendLine($"<p><strong>Address:</strong> {cafe.Address}</p>");
                }

                var gameCount = cafe.CafeGames?.Count ?? 0;
                if (gameCount > 0)
                {
                    sb.AppendLine($"<p><strong>Game Library:</strong> {gameCount}+ games</p>");
                }

                if (!string.IsNullOrEmpty(cafe.Description))
                {
                    var shortDesc = cafe.Description.Length > 200
                        ? cafe.Description.Substring(0, 197) + "..."
                        : cafe.Description;
                    sb.AppendLine($"<p>{shortDesc}</p>");
                }

                sb.AppendLine("</div>"); // Close content column
                sb.AppendLine("</div>"); // Close row
                sb.AppendLine("</div>"); // Close cafe-card
            }

            sb.AppendLine("<h2>Tips for Visiting</h2>");
            sb.AppendLine("<ul>");
            sb.AppendLine("<li><strong>Make a reservation:</strong> Popular cafes can fill up quickly, especially on weekends.</li>");
            sb.AppendLine("<li><strong>Ask for recommendations:</strong> Staff are usually gamers themselves and can suggest the perfect game for your group.</li>");
            sb.AppendLine("<li><strong>Plan your time:</strong> Most games take 1-3 hours. Give yourself plenty of time to enjoy.</li>");
            sb.AppendLine("<li><strong>Try something new:</strong> Step outside your comfort zone and try a game you've never played before!</li>");
            sb.AppendLine("</ul>");

            sb.AppendLine("<h2>Explore More</h2>");
            sb.AppendLine($"<p><a href=\"/?city={Uri.EscapeDataString(city)}\" class=\"btn btn-primary\">View All Cafes in {city}</a></p>");

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

            // Remove special characters
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

            // Replace spaces with hyphens
            slug = Regex.Replace(slug, @"\s+", "-");

            // Remove multiple consecutive hyphens
            slug = Regex.Replace(slug, @"-+", "-");

            // Trim hyphens from ends
            slug = slug.Trim('-');

            // Limit length
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
