using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Services.Interfaces;

[Route("api/test-email")]
[ApiController]
public class TestEmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<TestEmailController> _logger;

    public TestEmailController(IEmailService emailService, IConfiguration config, ILogger<TestEmailController> logger)
    {
        _emailService = emailService;
        _config = config;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetEmailConfig()
    {
        var config = new
        {
            SmtpHost = _config["Email:SmtpHost"],
            SmtpPort = _config["Email:SmtpPort"],
            Username = _config["Email:Username"],
            HasPassword = !string.IsNullOrEmpty(_config["Email:Password"]),
            PasswordLength = _config["Email:Password"]?.Length ?? 0,
            FromEmail = _config["Email:FromEmail"],
            FromName = _config["Email:FromName"]
        };

        return Ok(config);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            _logger.LogInformation("Test email request received for {ToEmail}", request.ToEmail);

            await _emailService.SendAsync(
                request.ToEmail,
                request.ToName ?? "Test User",
                "Test Email from CedarPay",
                "<h1>✅ Email Working!</h1><p>If you receive this, your email configuration is correct.</p>" +
                "<p>Time: " + DateTime.Now + "</p>"
            );

            return Ok(new { success = true, message = "Email sent successfully!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test email failed");
            return BadRequest(new { success = false, error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    [HttpPost("test-direct")]
    public async Task<IActionResult> TestDirectSmtp()
    {
        try
        {
            using var client = new MailKit.Net.Smtp.SmtpClient();
            await client.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);

            var username = _config["Email:Username"];
            var password = _config["Email:Password"];

            await client.AuthenticateAsync(username, password);

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress("CedarPay Test", username));
            message.To.Add(new MimeKit.MailboxAddress("Test", "edwinmouawad82@gmail.com")); 
            message.Subject = "Direct SMTP Test";
            message.Body = new MimeKit.TextPart("plain") { Text = "This is a direct SMTP test" };

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return Ok(new { success = true, message = "Direct SMTP test successful!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message, fullException = ex.ToString() });
        }
    }
}

public class TestEmailRequest
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }
}