using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum ScheduleFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    public class ScheduledTransfer
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = "";

        public User User { get; set; } = null!;

        public int SenderWalletId { get; set; }
        public Wallet SenderWallet { get; set; } = null!;

        public string? ReceiverWalletSerial { get; set; }
        public string? ReceiverPhoneNumber { get; set; }
        public string? ReceiverName { get; set; }

        [Required]
        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public ScheduleFrequency Frequency { get; set; }

        public DateTime NextRunAt { get; set; }

        public DateTime? LastRunAt { get; set; }

        public int ExecutionCount { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}