using CookingClassFinder.Models.Domain;

namespace CookingClassFinder.Services
{
    public interface IBlogService
    {
        Task<List<BlogPost>> GetPublishedPostsAsync(int page = 1, int pageSize = 10);
        Task<BlogPost?> GetPostBySlugAsync(string slug);
        Task<BlogPost?> GetPostByIdAsync(int id);
        Task<BlogPost> CreatePostAsync(BlogPost post);
        Task<BlogPost> UpdatePostAsync(BlogPost post);
        Task<bool> DeletePostAsync(int id);
        Task<List<BlogPost>> GetPostsByCityAsync(string city);
        Task<List<BlogPost>> GetPostsByCuisineAsync(string cuisine);
        Task IncrementViewCountAsync(int id);
    }
}
