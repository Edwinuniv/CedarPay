using MoneyTransfer.Models;

namespace MoneyTransfer.ViewModels
{
    public class DashboardViewModel
    {
        public string FullName { get; set; } = "";
        public string AccountSerial { get; set; } = "";
        public string DefaultWalletSerial { get; set; } = "";
        public decimal TotalBalance { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public List<WalletSummaryViewModel> Wallets { get; set; } = new List<WalletSummaryViewModel>();
        public List<RecentTransactionViewModel> RecentTransactions { get; set; } = new List<RecentTransactionViewModel>();
        public decimal TotalSent { get; set; }
        public decimal TotalReceived { get; set; }
        public int SentThisMonth { get; set; }
        public int ReceivedThisMonth { get; set; }
        public int ActiveBeneficiaries { get; set; }
        public int BeneficiaryCountries { get; set; }
    }
}