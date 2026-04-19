using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Transaction?> GetBySerialNumberAsync(string serialNumber)
        {
            return await _context.Transactions
                .Include(t => t.SenderWallet)
                .Include(t => t.ReceiverWallet)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .FirstOrDefaultAsync(t => t.SerialNumber == serialNumber);
        }

        public async Task<Transaction?> GetTransactionWithDetailsAsync(int id)
        {
            return await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.Commission)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Transaction>> GetByWalletIdAsync(int walletId)
        {
            return await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.SenderWalletId == walletId
                         || t.ReceiverWalletId == walletId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>>GetSentByUserAsync(string userId)
        {
            return await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w != null ? w.User : null)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w != null ? w.User : null)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.SenderWallet != null &&
                            t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>>GetReceivedByUserAsync(string userId)
        {
            return await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w != null ? w.User : null)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w != null ? w.User : null)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.ReceiverWallet != null &&
                            t.ReceiverWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetByStatusAsync(TransactionStatus status)
        {
            return await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Where(t => t.Status == status)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUserTransactionCountAsync(string userId)
        {
            return await _context.Transactions
                .CountAsync(t => t.SenderWallet.UserId == userId);
        }

        public async Task<decimal> GetTotalSentByUserAsync(string userId)
        {
            return await _context.Transactions
                .Where(t => t.SenderWallet.UserId == userId
                         && t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.Amount);
        }

        public async Task<decimal> GetTotalReceivedByUserAsync(string userId)
        {
            return await _context.Transactions
                .Where(t => t.ReceiverWallet != null
                         && t.ReceiverWallet.UserId == userId
                         && t.Status == TransactionStatus.Completed)
                .SumAsync(t => t.ConvertedAmount);
        }

    }
}