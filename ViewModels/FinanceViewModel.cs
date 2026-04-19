namespace MoneyTransfer.ViewModels
{
    public class FinanceViewModel
    {
        public decimal TotalSent { get; set; }
        public decimal TotalReceived { get; set; }
        public decimal TotalTopUps { get; set; }
        public decimal TotalFeesPaid { get; set; }
        public decimal TotalFeesSaved { get; set; }
        public int TransactionCount { get; set; }
        public int FreeTransactionsUsed { get; set; }
        public int NextFreeIn { get; set; }
        public List<MonthlyFinanceViewModel> MonthlyBreakdown { get; set; } = new List<MonthlyFinanceViewModel>();
        public List<WalletSummaryViewModel> WalletBalances { get; set; } = new List<WalletSummaryViewModel>();
        public List<TopUpSummaryViewModel> RecentTopUps { get; set; } = new List<TopUpSummaryViewModel>();
    }
}