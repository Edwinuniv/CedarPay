using MoneyTransfer.Controllers;
using MoneyTransfer.Models;

namespace MoneyTransfer.ViewModels
{
    public class AnalyticsDashboardViewModel
    {
        public double AverageAppRating { get; set; }
        public int TotalAppReviews { get; set; }
        public Dictionary<int, int> AppRatingDistribution { get; set; }

        public double AverageAgentRating { get; set; }
        public int TotalAgentReviews { get; set; }
        public List<TopAgentViewModel> TopRatedAgents { get; set; }

        public double AverageTransactionRating { get; set; }
        public int TotalTransactionReviews { get; set; }

        public int TotalUsers { get; set; }
        public int ActiveUsersThisMonth { get; set; }
        public int NewUsersThisMonth { get; set; }

        public int TotalTransactions { get; set; }
        public int TotalTransactionsThisMonth { get; set; }
        public decimal TotalVolumeThisMonth { get; set; }
        public decimal TotalVolumeYear { get; set; }
        public decimal TotalFeesCollected { get; set; }
        public int TotalFreeTransactions { get; set; }

        public List<MonthlyStatViewModel> MonthlyStats { get; set; }

        public List<Review> RecentReviews { get; set; }
        public List<Transaction> RecentTransactions { get; set; }
        public List<User> RecentUsers { get; set; }
    }

}
