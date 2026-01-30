using EscapeRoomFinder.Data;
using EscapeRoomFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EscapeRoomFinder.Services
{
    public class BlogService : IBlogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IVenueService _venueService;
        private readonly ILogger<BlogService> _logger;

        public BlogService(
            ApplicationDbContext context,
            IVenueService venueService,
            ILogger<BlogService> logger)
        {
            _context = context;
            _venueService = venueService;
            _logger = logger;
        }

        public async Task<List<BlogPost>> GetPublishedPostsAsync(int page = 1, int pageSize = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished)
                .OrderByDescending(p => p.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<BlogPost?> GetPostBySlugAsync(string slug)
        {
            return await _context.BlogPosts
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);
        }

        public async Task<BlogPost?> GetPostByIdAsync(int id)
        {
            return await _context.BlogPosts.FindAsync(id);
        }

        public async Task<BlogPost> CreatePostAsync(BlogPost post)
        {
            post.Slug = GenerateSlug(post.Title);
            post.CreatedAt = DateTime.UtcNow;

            if (post.IsPublished && !post.PublishedAt.HasValue)
            {
                post.PublishedAt = DateTime.UtcNow;
            }

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();

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

            return post;
        }

        public async Task<bool> DeletePostAsync(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return false;

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<BlogPost>> GetPostsByCategoryAsync(string category, int limit = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.Category == category)
                .OrderByDescending(p => p.PublishedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<BlogPost>> GetPostsByCityAsync(string city, int limit = 10)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.RelatedCity == city)
                .OrderByDescending(p => p.PublishedAt)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetTotalPostCountAsync()
        {
            return await _context.BlogPosts.CountAsync(p => p.IsPublished);
        }

        public async Task IncrementViewCountAsync(int postId)
        {
            var post = await _context.BlogPosts.FindAsync(postId);
            if (post != null)
            {
                post.ViewCount++;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<BlogPost> GenerateCityGuideAsync(string city, string country)
        {
            var venues = await _context.Venues
                .Include(v => v.Rooms)
                .Where(v => v.IsActive && v.City.ToLower() == city.ToLower())
                .OrderByDescending(v => v.AverageRating)
                .Take(10)
                .ToListAsync();

            var totalRooms = venues.Sum(v => v.TotalRooms);
            var themes = venues.SelectMany(v => v.Rooms)
                .Where(r => r.IsActive)
                .Select(r => r.Theme)
                .Distinct()
                .ToList();

            var content = $@"
<h2>Escape Rooms in {city}</h2>
<p>{city} is home to {venues.Count} escape room venues with {totalRooms} unique rooms to explore.
Whether you're a puzzle enthusiast or a first-time player, you'll find the perfect challenge here.</p>

<h3>What to Expect</h3>
<p>Escape rooms in {city} offer a variety of themes including {string.Join(", ", themes.Take(5))}.
Most rooms accommodate 2-8 players and last 60 minutes.</p>

<h3>Top Venues</h3>
";
            foreach (var venue in venues.Take(5))
            {
                content += $@"
<h4>{venue.Name}</h4>
<p><strong>Location:</strong> {venue.Address}</p>
<p><strong>Rating:</strong> {venue.AverageRating:F1}/5 ({venue.TotalReviews} reviews)</p>
<p><strong>Rooms:</strong> {venue.TotalRooms} escape rooms</p>
<p>{venue.Description ?? "Experience thrilling escape room adventures at this popular venue."}</p>
";
            }

            content += $@"
<h3>Tips for First-Timers</h3>
<ul>
<li>Book in advance, especially for weekends</li>
<li>Arrive 15 minutes early for your briefing</li>
<li>Communicate with your team throughout the game</li>
<li>Don't be afraid to ask for hints</li>
<li>Most importantly, have fun!</li>
</ul>

<h3>Ready to Play?</h3>
<p>Browse all escape rooms in {city} and find your next adventure!</p>
";

            var post = new BlogPost
            {
                Title = $"Complete Guide to Escape Rooms in {city}, {country}",
                Slug = GenerateSlug($"escape-rooms-{city}-guide"),
                Summary = $"Discover the best escape rooms in {city}. Our comprehensive guide covers {venues.Count} venues and {totalRooms} rooms.",
                Content = content,
                Category = "city-guide",
                PostType = "city-guide",
                RelatedCity = city,
                RelatedCountry = country,
                Author = "Escape Room Finder",
                IsAutoGenerated = true,
                IsPublished = true,
                PublishedAt = DateTime.UtcNow,
                MetaTitle = $"Best Escape Rooms in {city} - Complete Guide {DateTime.UtcNow.Year}",
                MetaDescription = $"Find the best escape rooms in {city}. Browse {venues.Count} venues with {totalRooms} rooms. Read reviews, compare prices, and book your adventure."
            };

            return await CreatePostAsync(post);
        }

        public async Task<BlogPost> GenerateBestRoomsPostAsync(string city)
        {
            var rooms = await _context.Rooms
                .Include(r => r.Venue)
                .Where(r => r.IsActive && r.Venue.IsActive && r.Venue.City.ToLower() == city.ToLower())
                .OrderByDescending(r => r.AverageRating)
                .Take(10)
                .ToListAsync();

            var content = $@"
<h2>Top {rooms.Count} Escape Rooms in {city}</h2>
<p>Looking for the best escape room experiences in {city}? We've compiled the top-rated rooms
based on player reviews and ratings.</p>
";

            int rank = 1;
            foreach (var room in rooms)
            {
                content += $@"
<h3>#{rank}. {room.Name} at {room.Venue.Name}</h3>
<p><strong>Theme:</strong> {room.Theme}</p>
<p><strong>Difficulty:</strong> {room.GetDifficultyText()} ({room.Difficulty}/5)</p>
<p><strong>Players:</strong> {room.GetPlayerRange()}</p>
<p><strong>Duration:</strong> {room.DurationMinutes} minutes</p>
<p><strong>Rating:</strong> {room.AverageRating:F1}/5</p>
<p>{room.Description ?? "Challenge yourself with this exciting escape room experience."}</p>
";
                rank++;
            }

            var post = new BlogPost
            {
                Title = $"Top {rooms.Count} Best Escape Rooms in {city} ({DateTime.UtcNow.Year})",
                Slug = GenerateSlug($"best-escape-rooms-{city}-{DateTime.UtcNow.Year}"),
                Summary = $"Discover the top-rated escape rooms in {city}. From horror to adventure, find your perfect challenge.",
                Content = content,
                Category = "best-rooms",
                PostType = "best-escape-rooms",
                RelatedCity = city,
                Author = "Escape Room Finder",
                IsAutoGenerated = true,
                IsPublished = true,
                PublishedAt = DateTime.UtcNow,
                MetaTitle = $"Best Escape Rooms in {city} {DateTime.UtcNow.Year} - Top Rated",
                MetaDescription = $"Top {rooms.Count} highest-rated escape rooms in {city}. Read reviews, compare difficulty levels, and book your adventure."
            };

            return await CreatePostAsync(post);
        }

        private string GenerateSlug(string title)
        {
            var slug = title.ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            // Ensure uniqueness
            var baseSlug = slug;
            var counter = 1;
            while (_context.BlogPosts.Any(p => p.Slug == slug))
            {
                slug = $"{baseSlug}-{counter}";
                counter++;
            }

            return slug;
        }
    }
}
