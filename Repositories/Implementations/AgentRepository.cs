using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class AgentRepository : GenericRepository<Agent>, IAgentRepository
    {
        public AgentRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Agent>> GetApprovedAgentsAsync()
        {
            return await _context.Agents.Where(a => a.Status == AgentStatus.Approved).ToListAsync();
        }

        public async Task<IEnumerable<Agent>> GetPendingAgentsAsync()
        {
            return await _context.Agents
                .Include(a => a.User)
                .Where(a => a.Status == AgentStatus.Pending)
                .ToListAsync();
        }

        public async Task<IEnumerable<Agent>> GetByUserIdAsync(string userId)
        {
            return await _context.Agents.Where(a => a.UserId == userId).ToListAsync();
        }

        public async Task<IEnumerable<Agent>> GetByCityAsync(string city)
        {
            return await _context.Agents
                .Where(a => a.City == city
                         && a.Status == AgentStatus.Approved)
                .ToListAsync();
        }

        public async Task<IEnumerable<Agent>> GetByCountryAsync(string country)
        {
            return await _context.Agents.Where(a => a.Country == country && a.Status == AgentStatus.Approved).ToListAsync();
        }

        public async Task<Agent?> GetAgentWithCommissionsAsync(int id)
        {
            return await _context.Agents
                .Include(a => a.Commissions)
                .Include(a => a.Reviews)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task UpdateLocationAsync(int agentId, double latitude, double longitude)
        {
            var agent = await _context.Agents.FindAsync(agentId);
            if (agent != null)
            {
                agent.Latitude = latitude;
                agent.Longitude = longitude;
                await _context.SaveChangesAsync();
            }
        }
    }
}