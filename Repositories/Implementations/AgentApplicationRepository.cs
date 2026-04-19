using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class AgentApplicationRepository: GenericRepository<AgentApplication>, IAgentApplicationRepository
    {
        public AgentApplicationRepository(ApplicationDbContext context): base(context) { }

        public async Task<IEnumerable<AgentApplication>> GetByUserIdAsync(string userId)
        {
            return await _context.AgentApplications
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<AgentApplication>> GetPendingAsync()
        {
            return await _context.AgentApplications
                .Include(a => a.User)
                .Where(a => a.Status == ApplicationStatus.Pending)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync();
        }

        public async Task<AgentApplication?> GetByUserIdAndStatusAsync(string userId, ApplicationStatus status)
        {
            return await _context.AgentApplications
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Status == status);
        }

        public async Task<bool> HasPendingApplicationAsync(string userId)
        {
            return await _context.AgentApplications
                .AnyAsync(a => a.UserId == userId && a.Status == ApplicationStatus.Pending);
        }
    }
}