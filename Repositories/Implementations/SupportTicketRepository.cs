using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class SupportTicketRepository: GenericRepository<SupportTicket>, ISupportTicketRepository
    {
        public SupportTicketRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<SupportTicket>> GetByUserIdAsync(string userId)
        {
            return await _context.SupportTickets
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupportTicket>> GetByStatusAsync(TicketStatus status)
        {
            return await _context.SupportTickets
                .Include(s => s.User)
                .Where(s => s.Status == status)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<SupportTicket>> GetOpenTicketsAsync()
        {
            return await _context.SupportTickets
                .Include(s => s.User)
                .Where(s => s.Status == TicketStatus.Open
                         || s.Status == TicketStatus.InProgress)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
    }
}
