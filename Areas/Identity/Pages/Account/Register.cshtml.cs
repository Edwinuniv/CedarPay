// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace MoneyTransfer.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public RegisterModel(UserManager<User> userManager, IUserStore<User> userStore, SignInManager<User> signInManager, ILogger<RegisterModel> logger, IEmailSender emailSender, ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(30, MinimumLength = 3)]
            [RegularExpression(@"^[a-zA-Z0-9_]+$",
                ErrorMessage = "Username can only contain letters, numbers, and underscores")]
            public string Username { get; set; }

            [Required]
            public string FirstName { get; set; }

            [Required]
            public string LastName { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == Input.Username);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Input.Username",
                        "This username is already taken.");
                    return Page();
                }

                var user = CreateUser();

                await _userManager.SetUserNameAsync(user, Input.Username);
                await _userManager.SetEmailAsync(user, Input.Email);

                user.UserName = Input.Username.ToLower();
                user.FirstName = Input.FirstName;
                user.LastName = Input.LastName;
                user.CreatedAt = DateTime.Now;
                user.IsActive = true;
                user.ProfileCompleted = false;

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    user.FirstName = Input.FirstName;
                    user.LastName = Input.LastName;
                    user.CreatedAt = DateTime.Now;
                    user.IsActive = true;
                    user.ProfileCompleted = false;

                    var random = new Random();
                    var sixDigitCode = random.Next(100000, 999999).ToString();

                    user.EmailVerificationCode = sixDigitCode;
                    user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15);

                    await _userManager.UpdateAsync(user);
                    await _userManager.AddToRoleAsync(user, Roles.User);

                    await _emailSender.SendEmailAsync(Input.Email,
                        "Your CedarPay Verification Code",
                        $"<h2>Welcome to CedarPay!</h2>" +
                        $"<p>Your verification code is:</p>" +
                        $"<h1 style='font-size:48px;letter-spacing:8px;color:#00b894;text-align:center;'>{sixDigitCode}</h1>" +
                        $"<p>Enter this code on the verification page to activate your account.</p>" +
                        $"<p style='color:#888;'>This code expires in 15 minutes.</p>");

                    return RedirectToPage("./VerifyCode", new { email = Input.Email });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'. " +
                    $"Ensure that '{nameof(User)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<User> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<User>)_userStore;
        }
    }
}