// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using MoneyTransfer.Models;

namespace MoneyTransfer.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ResetPasswordModel> _logger;
        private readonly SignInManager<User> _signInManager;

        public ResetPasswordModel(
            UserManager<User> userManager,
            ILogger<ResetPasswordModel> logger,
            SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _logger = logger;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string code = null, string email = null)
        {
            _logger.LogInformation("=== RESET PASSWORD PAGE LOADED ===");
            _logger.LogInformation($"Code provided: {!string.IsNullOrEmpty(code)}");
            _logger.LogInformation($"Email provided: {!string.IsNullOrEmpty(email)}");

            if (code == null)
            {
                _logger.LogError("No reset code provided");
                TempData["Error"] = "Invalid or missing reset code. Please request a new password reset.";
                return RedirectToPage("./ForgotPassword");
            }

            try
            {
                var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));

                Input = new InputModel
                {
                    Code = decodedCode,
                    Email = email ?? ""
                };

                _logger.LogInformation("Reset password page loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decode reset code");
                TempData["Error"] = "Invalid reset link. Please request a new password reset.";
                return RedirectToPage("./ForgotPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("=== RESET PASSWORD SUBMITTED ===");
            _logger.LogInformation($"Email: {Input?.Email}");
            _logger.LogInformation($"Code provided: {!string.IsNullOrEmpty(Input?.Code)}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"Validation error: {error.ErrorMessage}");
                }
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                _logger.LogWarning($"User not found with email: {Input.Email}");
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            _logger.LogInformation($"User found: {user.Email}");

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Password reset successful for {user.Email}");

                if (!user.EmailConfirmed)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    await _userManager.ConfirmEmailAsync(user, token);
                    _logger.LogInformation($"Email confirmed for {user.Email}");
                }

                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(1));

                TempData["Success"] = "Your password has been reset successfully! Please log in with your new password.";
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                _logger.LogError($"Reset password error: {error.Description}");
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}