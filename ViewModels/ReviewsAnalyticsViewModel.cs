using MoneyTransfer.Controllers;
using MoneyTransfer.Models;

namespace MoneyTransfer.ViewModels
{
    public class ReviewsAnalyticsViewModel
    {
        public List<Review> AppReviews { get; set; }
        public List<Review> AgentReviews { get; set; }
        public List<Review> TransactionReviews { get; set; }
        public List<TopAgentViewModel> TopAgents { get; set; }
        public double AverageAppRating { get; set; }
        public double AverageAgentRating { get; set; }
        public double AverageTransactionRating { get; set; }
    }
}
