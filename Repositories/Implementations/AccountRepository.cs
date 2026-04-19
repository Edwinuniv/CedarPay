using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class AccountRepository : GenericRepository<Account>, IAccountRepository
    {
        public AccountRepository(ApplicationDbContext context) : base(context)
        {
        }
        public async Task<Account?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.SerialNumber == serialNumber);
        }
        public async Task<Account?> GetAccountWithWalletsAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.Wallets)
                    .ThenInclude(w => w.Currency)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        public async Task<IEnumerable<Account>> GetByUserIdAsync(string userId)
        {
            return await _context.Accounts
                .Include(a => a.Wallets)
                    .ThenInclude(w => w.Currency)
                .Where(a => a.UserId == userId)
                .ToListAsync();
        }
        public async Task<Account?> GetDefaultAccountAsync(string userId)
        {
            return await _context.Accounts
                .Include(a => a.Wallets)
                .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
        }
    }
}
