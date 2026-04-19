using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class Account
    {
        public int Id { get; set; }
        [Required]
        public string SerialNumber { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }

        [Required]
        public string UserId { get; set; }

        public User User { get; set; }
        public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();

    }
}
