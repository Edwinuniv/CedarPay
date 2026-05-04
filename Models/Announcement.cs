using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum AnnouncementType
    {
        Info,
        Warning,
        Success,
        Maintenance
    }

    public class Announcement
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = "";

        [Required]
        public string Content { get; set; } = "";

        public AnnouncementType Type { get; set; } = AnnouncementType.Info;

        public bool IsActive { get; set; } = true;
        public bool ShowToAll { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiresAt { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = "";
        public User? CreatedBy { get; set; }
    }
}
