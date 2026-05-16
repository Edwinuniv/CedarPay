namespace MoneyTransfer.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendAsync(string toEmail, string toName, string subject, string htmlBody);
        Task SendNotificationAsync(string toEmail, string toName, string title, string message);
        Task SendChatMessageAsync(string toEmail, string toName, string fromName, string messageContent);
        Task SendTransactionReceiptAsync(string toEmail, string toName, string serialNumber, decimal amount, string currencySymbol, string currency, string recipientName, decimal fee, bool feeWaived, DateTime date, string? description, string category);
    }
}