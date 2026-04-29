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
            try
            {
                var host = _config["Email:SmtpHost"] ?? "";
                var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var username = _config["Email:Username"] ?? "";
                var password = _config["Email:Password"] ?? "";
                var fromName = _config["Email:FromName"] ?? "CedarPay";
                var fromEmail = _config["Email:FromEmail"] ?? username;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("Email not configured. " + "Skipping email to {Email}", toEmail);
                    return;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = GetEmailTemplate(subject, htmlBody)
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            }
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
                <h2 style='color:#00b894'>
                    New message from {fromName}
                </h2>
                <div style='background:#f5f7fa;
                            padding:16px;
                            border-radius:8px;
                            border-left:4px solid #00b894;
                            font-size:15px;
                            margin:16px 0'>
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

        private static string GetEmailTemplate(string subject, string content)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI',
                        sans-serif; background: #f5f7fa;
                        margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px;
                        margin: 0 auto;
                        background: #fff;
                        border-radius: 16px;
                        overflow: hidden; }}
                    .header {{ background: linear-gradient(
                        135deg, #00b894, #00cec9);
                        padding: 32px 40px;
                        text-align: center; }}
                    .brand {{ font-size: 28px;
                        font-weight: 800; color: #fff; }}
                    .content {{ padding: 32px 40px;
                        color: #1a1a2e; }}
                    .footer {{ padding: 20px 40px;
                        text-align: center;
                        font-size: 12px; color: #888;
                        border-top: 1px solid #f0f0f0; }}
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