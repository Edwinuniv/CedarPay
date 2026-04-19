using Microsoft.AspNetCore.Mvc;

namespace MoneyTransfer.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Dashboard");

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}