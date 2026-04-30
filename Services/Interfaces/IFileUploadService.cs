using MoneyTransfer.Models;

namespace MoneyTransfer.Services.Interfaces
{
    public interface IFileUploadService
    {
        Task<(string url, string fileName, long fileSize)> SaveChatFileAsync(IFormFile file);
        bool IsImage(IFormFile file);
        bool IsVideo(IFormFile file);
        bool IsVoice(IFormFile file);
        bool IsAllowedFile(IFormFile file);
        MessageType GetMessageType(IFormFile file);
    }
}