using EscapeRoomFinder.Models.Domain;

namespace EscapeRoomFinder.Services
{
    public interface IBlogService
    {
        Task<List<BlogPost>> GetPublishedPostsAsync(int page = 1, int pageSize = 10);
        Task<BlogPost?> GetPostBySlugAsync(string slug);
        Task<BlogPost?> GetPostByIdAsync(int id);
        Task<BlogPost> CreatePostAsync(BlogPost post);
        Task<BlogPost> UpdatePostAsync(BlogPost post);
        Task<bool> DeletePostAsync(int id);
        Task<List<BlogPost>> GetPostsByCategoryAsync(string category, int limit = 10);
        Task<List<BlogPost>> GetPostsByCityAsync(string city, int limit = 10);
        Task<int> GetTotalPostCountAsync();
        Task IncrementViewCountAsync(int postId);

        // Auto-generation
        Task<BlogPost> GenerateCityGuideAsync(string city, string country);
        Task<BlogPost> GenerateBestRoomsPostAsync(string city);
    }
}
