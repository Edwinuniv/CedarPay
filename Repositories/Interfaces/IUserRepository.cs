using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(string id);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByUserWithDetailsAsync(string id);
        Task<IEnumerable<User>> GetAllAsync();
        Task UpdateAsync(User user);
        Task<bool> ExistsAsync(string id);
    }
}
