using BoardGameCafeFinder.Models.Domain;

namespace BoardGameCafeFinder.Services
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

        // Auto-Generation (culture parameter for multi-language support)
        Task<BlogPost> GenerateTopGamesPostAsync(string city, string? country = null, string? culture = null);
        Task<List<BlogPost>> GenerateTopGamesPostsForCitiesAsync(List<string> cities);
        Task<BlogPost> GenerateCityGuidePostAsync(string city, string? country = null, string? culture = null);

        // Utilities
        Task IncrementViewCountAsync(int id);
        string GenerateSlug(string title);
    }
}
