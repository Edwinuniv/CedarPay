namespace MoneyTransfer.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
        Task SendNotificationAsync(string toEmail, string toName, string title, string message);
        Task SendChatMessageAsync(string toEmail, string toName, string fromName, string messageContent);
    }
}