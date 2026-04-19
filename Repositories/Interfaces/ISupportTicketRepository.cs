using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface ISupportTicketRepository: IGenericRepository<SupportTicket>
    {
        Task<IEnumerable<SupportTicket>> GetByUserIdAsync(string userId);
        Task<IEnumerable<SupportTicket>> GetByStatusAsync(TicketStatus status);
         Task<IEnumerable<SupportTicket>> GetOpenTicketsAsync();
    }
}
