#nullable disable
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using MoneyTransfer.Models;
using MoneyTransfer.Services.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<User> userManager,
            IEmailService emailService,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("=== FORGOT PASSWORD POST STARTED ===");
            _logger.LogInformation("Email submitted: {Email}", Input?.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            _logger.LogInformation("User found: {UserFound}", user != null);

            if (user != null)
            {
                try
                {
                    _logger.LogInformation("Generating password reset token for {Email}", Input.Email);

                    // Generate reset token
                    var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ResetPassword",
                        pageHandler: null,
                        values: new { area = "Identity", code },
                        protocol: Request.Scheme);

                    _logger.LogInformation("Reset URL generated: {Url}", callbackUrl);

                    var html = $@"
                        <!DOCTYPE html>
                        <html>
                        <head>
                            <meta charset='utf-8'>
                            <style>
                                body {{ font-family: Arial, sans-serif; }}
                                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                                .button {{ background-color: #00b894; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; display: inline-block; }}
                            </style>
                        </head>
                        <body>
                            <div class='container'>
                                <h2 style='color:#00b894'>Reset Your Password</h2>
                                <p>Hello {user.FirstName} {user.LastName},</p>
                                <p>We received a request to reset the password for your CedarPay account.</p>
                                <p>Click the button below to set a new password:</p>
                                <p style='text-align:center;margin:30px 0'>
                                    <a href='{HtmlEncoder.Default.Encode(callbackUrl)}' class='button' style='background:#00b894;color:#fff;padding:14px 32px;border-radius:10px;text-decoration:none;font-weight:700;font-size:16px'>
                                        Reset Password
                                    </a>
                                </p>
                                <p>If you didn't request this, you can safely ignore this email.</p>
                                <p>This link expires in 24 hours.</p>
                                <hr>
                                <p style='font-size:12px;color:#888'>CedarPay - Lebanon's Digital Wallet</p>
                            </div>
                        </body>
                        </html>";

                    _logger.LogInformation("Sending email to {Email}...", Input.Email);

                    // Send email directly - NO Task.Run
                    await _emailService.SendAsync(
                        Input.Email,
                        $"{user.FirstName} {user.LastName}",
                        "CedarPay — Reset Your Password",
                        html);

                    _logger.LogInformation("✅ Password reset email sent successfully to {Email}", Input.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to send password reset email to {Email}", Input.Email);
                    // Don't throw - we don't want to reveal that user exists
                }
            }
            else
            {
                _logger.LogWarning("User not found for email: {Email}", Input.Email);
            }

            _logger.LogInformation("Redirecting to confirmation page");
            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}