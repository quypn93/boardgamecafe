namespace CookingClassFinder.Services
{
    public interface IImageStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, string folder);
        Task<bool> DeleteImageAsync(string path);
        string GetImageUrl(string path);
    }
}
