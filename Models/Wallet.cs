using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class Wallet
    {
        public int Id { get; set; }
        [Required]
        public string SerialNumber { get; set; }
        public decimal Balance { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }

        [Required]
        public string UserId { get; set; }
        public int AccountId { get; set; }
        public int CurrencyId { get; set; }

        public User User { get; set; }
        public Account Account { get; set; }
        public Currency Currency { get; set; }
        public ICollection<Transaction> SentTransactions { get; set; } = new List<Transaction>();
        public ICollection<Transaction> ReceivedTransactions { get; set; } = new List<Transaction>();
        public ICollection<TopUp> TopUps { get; set; } = new List<TopUp>();
    }
}
