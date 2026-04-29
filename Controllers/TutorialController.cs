using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    public class TutorialController : BaseController
    {
        private readonly UserManager<User> _userManager;

        public TutorialController(
            UserManager<User> userManager,
            IUserRepository userRepository)
            : base(userManager, userRepository)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Guest()
        {
            ViewBag.GuestBalance = 10000m;
            ViewBag.GuestCurrency = "USD";
            ViewBag.GuestWalletSerial = "WAL-DEMO-00001";
            return View();
        }
    }
}