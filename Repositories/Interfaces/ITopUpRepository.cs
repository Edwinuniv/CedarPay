using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface ITopUpRepository: IGenericRepository<TopUp>
    {
        Task<IEnumerable<TopUp>> GetByWalletIdAsync(int walletId);
        Task<IEnumerable<TopUp>> GetByUserIdAsync(string userId);
        Task<IEnumerable<TopUp>> GetByStatusAsync(TopUpStatus status);
        Task<decimal> GetTotalToppedUpByUserAsync(string userId);
    }
}
