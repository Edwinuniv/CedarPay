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
    public class NotificationController : BaseController
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;

        public NotificationController(
            INotificationRepository notificationRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            IEmailService emailService) : base(userManager, userRepository)
        {
            _notificationRepository = notificationRepository;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationRepository
                .GetByUserIdAsync(userId);

            var vm = notifications.Select(n => new NotificationViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                IsRead = n.IsRead,
                RedirectUrl = n.RedirectUrl,
                CreatedAt = n.CreatedAt
            }).ToList();

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationRepository.MarkAsReadAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            await _notificationRepository.MarkAllAsReadAsync(userId);
            TempData["Success"] = "All notifications marked as read!";
            return RedirectToAction("Index");
        }
    }
}