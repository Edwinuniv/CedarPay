using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum ActivityType
    {
        Login,
        Logout,
        TransferSent,
        TransferReceived,
        TopUp,
        ProfileUpdated,
        PasswordChanged,
        KYCSubmitted,
        WalletCreated,
        BeneficiaryAdded,
        BeneficiaryRemoved,
        SettingsChanged
    }

    public class ActivityLog
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public User User { get; set; } = null!;

        public ActivityType Action { get; set; }

        public string? Details { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}