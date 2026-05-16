using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum BeneficiaryType
    {
        WalletTransfer,
        MobileTransfer,
    }

    public class Beneficiary
    {
        public int Id { get; set; }
        [Required]
        public string Nickname { get; set; }
        [Required]
        public string ReceiverName { get; set; }
        public string? ReceiverPhoneNumber { get; set; }
        public string? ReceiverWalletSerial { get; set; }
        public string? ReceiverCountry { get; set; }
        public string? ReceiverCity { get; set; }
        public string? ReceiverBankName { get; set; }
        public BeneficiaryType Type { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? ReceiverUserId { get; set; }
        public User? ReceiverUser { get; set; }

        [Required]
        public string UserId { get; set; }

        public User User { get; set; }
    }
}