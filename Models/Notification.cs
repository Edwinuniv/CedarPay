using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum NotificationType
    {
        TransactionSent,
        TransactionReceived,
        TopUpCompleted,
        WalletCreated,
        KYCApproved,
        KYCRejected,
        General
    }
    public class Notification
    {
        public int Id { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Message { get; set; }
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; }
        public string? RedirectUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        [Required]
        public string UserId { get; set; }

        public User User { get; set; }
    }
}
