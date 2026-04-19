using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    public class BaseController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly IUserRepository _userRepository;

        public BaseController(UserManager<User> userManager, IUserRepository userRepository)
        {
            _userManager = userManager;
            _userRepository = userRepository;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = _userManager.GetUserId(User);
                if (userId != null)
                {
                    var user = await _userRepository.GetByIdAsync(userId);
                    if (user != null)
                    {
                        ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
                        ViewBag.FullName = $"{user.FirstName} {user.LastName}";
                        ViewBag.UserInitials = $"{user.FirstName?.Substring(0, 1)}" + $"{user.LastName?.Substring(0, 1)}";
                    }
                }
            }
            await next();
        }
    }
}