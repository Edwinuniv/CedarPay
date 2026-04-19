using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class ReviewRepository: GenericRepository<Review>, IReviewRepository
    {
        public ReviewRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Review>> GetByUserIdAsync(string userId)
        {
            return await _context.Reviews
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Review>> GetByAgentIdAsync(int agentId)
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.AgentId == agentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Review>> GetAppReviewsAsync()
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.Target == ReviewTarget.App)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<double> GetAverageRatingForAgentAsync(int agentId)
        {
            var hasReviews = await _context.Reviews
                .AnyAsync(r => r.AgentId == agentId);

            if (!hasReviews) return 0;

            return await _context.Reviews
                .Where(r => r.AgentId == agentId)
                .AverageAsync(r => r.Rating);
        }

        public async Task<double> GetAverageAppRatingAsync()
        {
            var hasReviews = await _context.Reviews
                .AnyAsync(r => r.Target == ReviewTarget.App);

            if (!hasReviews) return 0;

            return await _context.Reviews
                .Where(r => r.Target == ReviewTarget.App)
                .AverageAsync(r => r.Rating);
        }
    }
}
