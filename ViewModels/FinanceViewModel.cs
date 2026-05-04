namespace MoneyTransfer.ViewModels
{
    public class FinanceViewModel
    {
        public int? SelectedWalletId { get; set; }
        public string SelectedWalletCurrencySymbol { get; set; } = "$";
        public string SelectedWalletCurrencyCode { get; set; } = "";

        public decimal TotalSent { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal TotalTopUps { get; set; }
        public decimal TotalFeesPaid { get; set; }
        public decimal TotalFeesSaved { get; set; }
        public int TransactionCount { get; set; }
        public int FreeTransactionsUsed { get; set; }
        public int NextFreeIn { get; set; }

        public List<MonthlyFinanceViewModel> MonthlyBreakdown { get; set; } = new();
        public List<WalletSummaryViewModel> WalletBalances { get; set; } = new();
        public List<TopUpSummaryViewModel> RecentTopUps { get; set; } = new();
        public List<FinanceHistoryItemViewModel> FullHistory { get; set; } = new();
    }
}