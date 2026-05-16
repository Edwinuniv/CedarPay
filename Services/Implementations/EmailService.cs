using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var host = _config["Email:SmtpHost"] ?? "";
            var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var username = _config["Email:Username"] ?? "";
            var password = _config["Email:Password"] ?? "";
            var fromName = _config["Email:FromName"] ?? "CedarPay";
            var fromEmail = _config["Email:FromEmail"] ?? username;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Email credentials not configured");
            }
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = GetEmailTemplate(subject, htmlBody) };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendNotificationAsync(string toEmail, string toName, string title, string message)
        {
            var html = $@"
                <h2 style='color:#00b894'>{title}</h2>
                <p style='font-size:16px'>{message}</p>
                <hr/>
                <a href='https://localhost:5125/Notification/Index'
                   style='background:#00b894;color:#fff;
                          padding:12px 24px;border-radius:8px;
                          text-decoration:none;font-weight:600'>
                    View Notifications
                </a>";

            await SendAsync(toEmail, toName, $"CedarPay: {title}", html);
        }

        public async Task SendChatMessageAsync(string toEmail, string toName, string fromName, string messageContent)
        {
            var html = $@"
                <h2 style='color:#00b894'>New message from {fromName}</h2>
                <div style='background:#f5f7fa;padding:16px;border-radius:8px;
                            border-left:4px solid #00b894;font-size:15px;margin:16px 0'>
                    {messageContent}
                </div>
                <a href='https://localhost:5125/Chat/Index'
                   style='background:#00b894;color:#fff;
                          padding:12px 24px;border-radius:8px;
                          text-decoration:none;font-weight:600'>
                    Reply on CedarPay
                </a>";

            await SendAsync(toEmail, toName, $"CedarPay: New message from {fromName}", html);
        }

        public async Task SendTransactionReceiptAsync(string toEmail, string toName, string serialNumber, decimal amount, string currencySymbol, string currency, string recipientName, decimal fee, bool feeWaived, DateTime date, string? description, string category)
        {
            var feeHtml = feeWaived
                ? "<span style='color:#00b894;font-weight:700'>FREE 🎉</span>"
                : $"{currencySymbol}{fee:N2}";

            var descHtml = string.IsNullOrEmpty(description)
                ? ""
                : $@"<tr>
                       <td style='padding:10px 0;color:#888;border-bottom:1px solid #f0f0f0'>Note</td>
                       <td style='padding:10px 0;font-weight:600;text-align:right;border-bottom:1px solid #f0f0f0'>{description}</td>
                     </tr>";

            var html = $@"
                <div style='text-align:center;padding:20px 0 28px'>
                    <div style='width:64px;height:64px;background:linear-gradient(135deg,#00b894,#00cec9);
                                border-radius:50%;display:inline-flex;align-items:center;
                                justify-content:center;margin-bottom:16px'>
                        <span style='font-size:28px'>✓</span>
                    </div>
                    <h2 style='margin:0 0 6px;color:#1a1a2e'>Transfer Successful</h2>
                    <p style='color:#888;margin:0'>Your money is on its way</p>
                    <div style='font-size:36px;font-weight:800;color:#1a1a2e;margin:20px 0 4px'>
                        {currencySymbol}{amount:N2} <span style='font-size:18px;color:#888'>{currency}</span>
                    </div>
                    <div style='color:#888;font-size:14px'>Sent to {recipientName}</div>
                </div>
                <div style='background:#f9fafb;border-radius:12px;padding:20px;margin-top:8px'>
                    <table width='100%' cellpadding='0' cellspacing='0' style='font-size:14px'>
                        <tr>
                            <td style='padding:10px 0;color:#888;border-bottom:1px solid #f0f0f0'>Transaction ID</td>
                            <td style='padding:10px 0;font-weight:600;text-align:right;border-bottom:1px solid #f0f0f0;font-family:monospace'>{serialNumber}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 0;color:#888;border-bottom:1px solid #f0f0f0'>Category</td>
                            <td style='padding:10px 0;font-weight:600;text-align:right;border-bottom:1px solid #f0f0f0'>{category}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 0;color:#888;border-bottom:1px solid #f0f0f0'>Fee</td>
                            <td style='padding:10px 0;text-align:right;border-bottom:1px solid #f0f0f0'>{feeHtml}</td>
                        </tr>
                        {descHtml}
                        <tr>
                            <td style='padding:10px 0;color:#888'>Date &amp; Time</td>
                            <td style='padding:10px 0;font-weight:600;text-align:right'>{date:MMMM dd, yyyy HH:mm}</td>
                        </tr>
                    </table>
                </div>
                <div style='text-align:center;margin-top:28px'>
                    <a href='https://localhost:5125/Transaction/Index'
                       style='background:#00b894;color:#fff;padding:14px 32px;border-radius:10px;
                              text-decoration:none;font-weight:700;font-size:15px;display:inline-block'>
                        View All Transactions
                    </a>
                </div>";

            await SendAsync(toEmail, toName, $"CedarPay Receipt — {serialNumber}", html);
        }

        private static string GetEmailTemplate(string subject, string content)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI', sans-serif; background: #f5f7fa; margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background: #fff; border-radius: 16px; overflow: hidden; }}
                    .header {{ background: linear-gradient(135deg, #00b894, #00cec9); padding: 32px 40px; text-align: center; }}
                    .brand {{ font-size: 28px; font-weight: 800; color: #fff; }}
                    .content {{ padding: 32px 40px; color: #1a1a2e; }}
                    .footer {{ padding: 20px 40px; text-align: center; font-size: 12px; color: #888; border-top: 1px solid #f0f0f0; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <div class='brand'>🌲 CedarPay</div>
                    </div>
                    <div class='content'>
                        {content}
                    </div>
                    <div class='footer'>
                        <p>CedarPay — Lebanon's Digital Wallet</p>
                        <p>Licensed by Banque du Liban</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}