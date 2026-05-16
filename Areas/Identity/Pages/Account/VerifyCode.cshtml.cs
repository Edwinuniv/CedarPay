// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneyTransfer.Models;

namespace MoneyTransfer.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class VerifyCodeModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;

        public VerifyCodeModel(UserManager<User> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Code { get; set; }

        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string email = null)
        {
            if (string.IsNullOrEmpty(email))
                return RedirectToPage("./Login");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound();

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["SuccessMessage"] = "Email already verified. Please login.";
                return RedirectToPage("./Login");
            }

            Email = email;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Code) || Code.Length != 6)
            {
                ErrorMessage = "Please enter a valid 6-digit code.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            if (user.EmailVerificationCode == Code &&
                user.EmailVerificationCodeExpiry > DateTime.UtcNow)
            {
                user.EmailConfirmed = true;
                user.EmailVerificationCode = null;
                user.EmailVerificationCodeExpiry = null;
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Email verified! You can now login.";
                return RedirectToPage("./Login");
            }
            else if (user.EmailVerificationCodeExpiry <= DateTime.UtcNow)
            {
                ErrorMessage = "Code has expired. Please request a new one.";
            }
            else
            {
                ErrorMessage = "Invalid code. Please try again.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null || await _userManager.IsEmailConfirmedAsync(user))
                return RedirectToPage("./Login");

            var random = new Random();
            var sixDigitCode = random.Next(100000, 999999).ToString();

            user.EmailVerificationCode = sixDigitCode;
            user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);
            await _userManager.UpdateAsync(user);

            await _emailSender.SendEmailAsync(Email,
                "Your New CedarPay Verification Code",
                $"<h2>New Verification Code</h2>" +
                $"<h1 style='font-size:48px;letter-spacing:8px;color:#00b894;text-align:center;'>{sixDigitCode}</h1>" +
                $"<p style='color:#888;'>This code expires in 15 minutes.</p>");

            TempData["SuccessMessage"] = "New code sent!";
            return RedirectToPage("./VerifyCode", new { email = Email });
        }
    }
}