using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface ITransactionRepository: IGenericRepository<Transaction>
    {
        Task<Transaction?> GetBySerialNumberAsync(string serialNumber);
        Task<Transaction?> GetTransactionWithDetailsAsync(int id);
        Task<IEnumerable<Transaction>> GetByWalletIdAsync(int walletId);
        Task<IEnumerable<Transaction>> GetSentByUserAsync(string userId);
        Task<IEnumerable<Transaction>> GetReceivedByUserAsync(string userId);
        Task<IEnumerable<Transaction>> GetByStatusAsync(TransactionStatus status);
        Task<int> GetUserTransactionCountAsync(string userId);
        Task<decimal> GetTotalSentByUserAsync(string userId);
        Task<decimal> GetTotalReceivedByUserAsync(string userId);
    }
}
