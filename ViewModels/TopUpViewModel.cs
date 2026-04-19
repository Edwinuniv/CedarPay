using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class TopUpViewModel
    {
        [Required]
        public int WalletId { get; set; }
        [Required]
        public decimal Amount { get; set; }
        public string Method { get; set; } = "CreditCard";
        public string? Description { get; set; }
        public List<WalletSummaryViewModel> UserWallets { get; set; } = new List<WalletSummaryViewModel>();
    }
}
