using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class SettingsController : BaseController
    {
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailService _emailService;

        public SettingsController(UserManager<User> userManager, SignInManager<User> signInManager, IUserRepository userRepository,IEmailService emailService): base(userManager, userRepository)
        {
            _signInManager = signInManager;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager
                .ChangePasswordAsync(user,
                    vm.CurrentPassword, vm.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Password changed!";

            if (user.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Password Changed Successfully",
                        $"Your CedarPay account password was changed on {DateTime.Now:MMMM dd, yyyy} at {DateTime.Now:HH:mm}.\n\n" +
                        $"If you did not make this change, please contact our support team immediately at support@cedarpay.lb\n\n" +
                        $"You can also reset your password by clicking 'Forgot Password' on the login page.");
                });
            }

            return RedirectToAction("Index");
        }
    }
}