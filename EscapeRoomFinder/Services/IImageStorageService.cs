namespace EscapeRoomFinder.Services
{
    public interface IImageStorageService
    {
        Task<string> SaveImageAsync(IFormFile file, string folder);
        Task<string> SaveImageFromUrlAsync(string imageUrl, string folder, string fileName);
        Task<bool> DeleteImageAsync(string relativePath);
        string GetImageUrl(string? relativePath);
    }
}
