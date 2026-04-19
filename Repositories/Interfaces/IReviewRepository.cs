using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IReviewRepository: IGenericRepository<Review>
    {
        Task<IEnumerable<Review>> GetByUserIdAsync(string userId);
        Task<IEnumerable<Review>> GetByAgentIdAsync(int agentId);
        Task<IEnumerable<Review>> GetAppReviewsAsync();
        Task<double> GetAverageRatingForAgentAsync(int agentId);
        Task<double> GetAverageAppRatingAsync();
    }
}
