using Microsoft.AspNetCore.Identity.UI.Services;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Services.Implementations
{
    public class IdentityEmailSender : IEmailSender
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<IdentityEmailSender> _logger;

        public IdentityEmailSender(IEmailService emailService, ILogger<IdentityEmailSender> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                var name = email.Split('@')[0];
                await _emailService.SendAsync(email, name, subject, htmlMessage);
                _logger.LogInformation($"Confirmation email sent to {email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {email}");
                throw;
            }
        }
    }
}