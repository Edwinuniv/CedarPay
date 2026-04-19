using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface INotificationRepository: IGenericRepository<Notification>
    {
        Task<IEnumerable<Notification>> GetByUserIdAsync(string userId);
        Task<IEnumerable<Notification>> GetUnreadByUserIdAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);
    }
}
