using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum WalletRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class WalletRequest
    {
        public int Id { get; set; }
        public WalletRequestStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public int CurrencyId { get; set; }
        public string? Description { get; set; }

        [Required]
        public string UserId { get; set; }

        public User User { get; set; }
        public Currency Currency { get; set; }
    }
}