using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class SettingsController : BaseController
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IUserRepository _userRepository;

        public SettingsController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IUserRepository userRepository)
            : base(userManager, userRepository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userRepository = userRepository;
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
            return RedirectToAction("Index");
        }
    }
}