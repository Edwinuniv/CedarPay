using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class TopUpRepository: GenericRepository<TopUp>, ITopUpRepository
    {
        public TopUpRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<TopUp>> GetByWalletIdAsync(int walletId)
        {
            return await _context.TopUps
                .Include(t => t.Currency)
                .Where(t => t.WalletId == walletId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<TopUp>> GetByUserIdAsync(string userId)
        {
            return await _context.TopUps
                .Include(t => t.Wallet)
                .Include(t => t.Currency)
                .Where(t => t.Wallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<TopUp>> GetByStatusAsync(TopUpStatus status)
        {
            return await _context.TopUps
                .Include(t => t.Wallet)
                .Where(t => t.Status == status)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalToppedUpByUserAsync(string userId)
        {
            return await _context.TopUps
                .Where(t => t.Wallet.UserId == userId
                         && t.Status == TopUpStatus.Completed)
                .SumAsync(t => t.Amount);
        }
    }
}
