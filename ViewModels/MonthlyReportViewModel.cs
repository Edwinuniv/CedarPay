using MoneyTransfer.Controllers;

namespace MoneyTransfer.ViewModels
{
    public class MonthlyReportViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; }

        public int NewUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalUsers { get; set; }

        public int TotalTransactions { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal TotalFees { get; set; }
        public int FreeTransactions { get; set; }

        public int NewAppReviews { get; set; }
        public int NewAgentReviews { get; set; }
        public int NewTransactionReviews { get; set; }
        public double AverageRatingThisMonth { get; set; }

        public List<TopAgentViewModel> TopAgents { get; set; }

        public List<DailyBreakdownViewModel> DailyBreakdown { get; set; }
    }
}
