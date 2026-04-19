using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IAgentApplicationRepository: IGenericRepository<AgentApplication>
    {
        Task<IEnumerable<AgentApplication>> GetByUserIdAsync(string userId);
        Task<IEnumerable<AgentApplication>> GetPendingAsync();
        Task<AgentApplication?> GetByUserIdAndStatusAsync(string userId, ApplicationStatus status);
        Task<bool> HasPendingApplicationAsync(string userId);
    }
}