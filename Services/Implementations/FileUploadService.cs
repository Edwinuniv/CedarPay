using MoneyTransfer.Models;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Services.Implementations
{
    public class FileUploadService : IFileUploadService
    {
        private readonly IWebHostEnvironment _env;

        private static readonly string[] ImageTypes =
        { 
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/bmp" 
        };

        private static readonly string[] VideoTypes =
        { 
            "video/mp4",
            "video/webm",
            "video/ogg",
             "video/quicktime",
            "video/x-msvideo" 
        };

        private static readonly string[] VoiceTypes =
        { 
            "audio/webm",
            "audio/ogg",
            "audio/mpeg",
             "audio/wav",
            "audio/mp4",
            "audio/aac" 
        };

        private static readonly string[] AllowedExtensions =
        { 
            ".jpg", 
            ".jpeg", 
            ".png", 
            ".gif",
            ".webp",
            ".mp4", 
            ".webm", 
            ".mov", 
            ".avi",
            ".pdf",
            ".doc", 
            ".docx", 
            ".xls", 
            ".xlsx",
            ".txt",
            ".zip",
            ".rar",
            ".ogg",
            ".wav",
            ".mp3", 
            ".aac" };

        public FileUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public bool IsImage(IFormFile file) => ImageTypes.Contains(file.ContentType.ToLower());

        public bool IsVideo(IFormFile file) => VideoTypes.Contains(file.ContentType.ToLower());

        public bool IsVoice(IFormFile file) => VoiceTypes.Contains(file.ContentType.ToLower());

        public bool IsAllowedFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            return AllowedExtensions.Contains(ext) || IsImage(file) || IsVideo(file) || IsVoice(file);
        }

        public MessageType GetMessageType(IFormFile file)
        {
            if (IsImage(file))
            {
                return MessageType.Image;
            }
            if (IsVideo(file))
            {
                return MessageType.Video;
            }
            if (IsVoice(file))
            {
                return MessageType.Voice;
            }
            return MessageType.File;
        }

        public async Task<(string url, string fileName, long fileSize)> SaveChatFileAsync(IFormFile file)
        {
            var msgType = GetMessageType(file);
            var subFolder = msgType switch
            {
                MessageType.Image => "images",
                MessageType.Video => "videos",
                MessageType.Voice => "voice",
                _ => "files"
            };

            var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "chat", subFolder);

            Directory.CreateDirectory(uploadPath);

            var ext = Path.GetExtension(file.FileName);
            var uniqueName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadPath, uniqueName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/uploads/chat/{subFolder}/{uniqueName}";
            return (url, file.FileName, file.Length);
        }
    }
}