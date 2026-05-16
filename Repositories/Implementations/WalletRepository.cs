using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class WalletRepository : GenericRepository<Wallet>, IWalletRepository
    {
        public WalletRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Wallet?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.Wallets.Include(w => w.Currency).FirstOrDefaultAsync(w => w.SerialNumber == serialNumber);
        }

        public async Task<IEnumerable<Wallet>> GetByUserIdAsync(string userId)
        {
            return await _context.Wallets.Include(w => w.Currency).Where(w => w.UserId == userId).ToListAsync();
        }

        public async Task<IEnumerable<Wallet>> GetByAccountIdAsync(int accountId)
        {
            return await _context.Wallets.Include(w => w.Currency).Where(w => w.AccountId == accountId).ToListAsync();
        }

        public async Task<Wallet?> GetWalletWithTransactionsAsync(int id)
        {
            return await _context.Wallets.Include(w => w.Currency).Include(w => w.SentTransactions).Include(w => w.ReceivedTransactions).FirstOrDefaultAsync(w => w.Id == id);
        }

        public async Task<Wallet?> GetDefaultWalletAsync(string userId)
        {
            return await _context.Wallets.Include(w => w.Currency).FirstOrDefaultAsync(w => w.UserId == userId && w.IsDefault);
        }

        public async Task<bool> HasSufficientBalenceAsync(int walletId, decimal amount)
        {
            var wallet = await _context.Wallets.FindAsync(walletId);
            return wallet != null && wallet.Balance >= amount;
        }

        public async Task<Wallet?> GetWithCurrencyAsync(int walletId)
        {
            return await _context.Wallets.Include(w => w.Currency).FirstOrDefaultAsync(w => w.Id == walletId);
        }
    }
}