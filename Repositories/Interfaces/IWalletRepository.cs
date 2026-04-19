using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IWalletRepository: IGenericRepository<Wallet>
    {
        Task<Wallet?> GetBySerialNumberAsync(string serialNumber);
        Task<IEnumerable<Wallet>> GetByUserIdAsync(string userId);
        Task<IEnumerable<Wallet>> GetByAccountIdAsync(int accountId);
         Task<Wallet?> GetDefaultWalletAsync(string userId);
        Task<bool> HasSufficientBalenceAsync(int walletId, decimal amount);
    }
}
