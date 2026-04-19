using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class TransferViewModel
    {
        [Required]
        public int SenderWalletId { get; set; }
        public string? ReceiverWalletSerial { get; set; }
        public string? ReceiverPhoneNumber { get; set; }
        public string? ReceiverName { get; set; }

        [Required]
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string TransferType { get; set; } = "WalletToWallet";

        public List<WalletSummaryViewModel> UserWallets { get; set; } = new List<WalletSummaryViewModel>();
        public List<BeneficiarySelectViewModel> Beneficiaries { get; set; } = new List<BeneficiarySelectViewModel>();
    
        public decimal EstimatedFee { get; set; }
        public decimal ExchangeRate { get; set; }
    }
}
