using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class BeneficiaryViewModel
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
        public string? TransferType { get; set; } = "WalletTransfer";
        public DateTime CreatedAt { get; set; }
        public string? ReceiverUserId { get; set; }
        public string? ReceiverProfilePictureUrl { get; set; }
        public bool IsFavorite { get; set; }
    }
}