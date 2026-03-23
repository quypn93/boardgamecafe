using CookingClassFinder.Data;
using CookingClassFinder.Models.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CookingClassFinder.Services
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
                post.PublishedAt = DateTime.UtcNow;

            _context.BlogPosts.Add(post);
            await _context.SaveChangesAsync();
            return post;
        }

        public async Task<BlogPost> UpdatePostAsync(BlogPost post)
        {
            post.UpdatedAt = DateTime.UtcNow;
            if (post.IsPublished && !post.PublishedAt.HasValue)
                post.PublishedAt = DateTime.UtcNow;

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

        public async Task<List<BlogPost>> GetPostsByCityAsync(string city)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.RelatedCity == city)
                .OrderByDescending(p => p.PublishedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<BlogPost>> GetPostsByCuisineAsync(string cuisine)
        {
            return await _context.BlogPosts
                .Where(p => p.IsPublished && p.RelatedCuisine == cuisine)
                .OrderByDescending(p => p.PublishedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task IncrementViewCountAsync(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post != null)
            {
                post.ViewCount++;
                await _context.SaveChangesAsync();
            }
        }

        private string GenerateSlug(string title)
        {
            var slug = title.ToLower();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

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
