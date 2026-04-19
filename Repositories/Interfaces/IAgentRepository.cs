using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface IAgentRepository: IGenericRepository<Agent>
    {
        Task<IEnumerable<Agent>> GetApprovedAgentsAsync();
        Task<IEnumerable<Agent>> GetPendingAgentsAsync();
        Task<Agent?> GetByUserIdAsync(string userId);
        Task<IEnumerable<Agent>> GetByCityAsync(string city);
        Task<IEnumerable<Agent>> GetByCountryAsync(string country);
        Task<Agent?> GetAgentWithCommissionsAsync(int id);
    }
}
