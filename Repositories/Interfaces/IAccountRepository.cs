using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IAccountRepository: IGenericRepository<Account>
    {
        Task<Account?> GetBySerialNumberAsync(string serialNumber);
        Task<Account?> GetAccountWithWalletsAsync(int id);
        Task<IEnumerable<Account>> GetByUserIdAsync(string userId);
        Task<Account?> GetDefaultAccountAsync(string userId);
    }
}
