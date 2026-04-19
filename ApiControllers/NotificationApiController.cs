using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationApiController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly UserManager<User> _userManager;

        public NotificationApiController(
            INotificationRepository notificationRepository,
            UserManager<User> userManager)
        {
            _notificationRepository = notificationRepository;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userId = _userManager.GetUserId(User);
            var notifications = await _notificationRepository
                .GetByUserIdAsync(userId);

            return Ok(notifications.Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                Type = n.Type.ToString(),
                n.IsRead,
                n.RedirectUrl,
                n.CreatedAt
            }));
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = _userManager.GetUserId(User);
            var count = await _notificationRepository.GetUnreadCountAsync(userId);
            return Ok(new { UnreadCount = count });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationRepository.MarkAsReadAsync(id);
            return Ok(new { message = "Notification marked as read." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            await _notificationRepository.MarkAllAsReadAsync(userId);
            return Ok(new { message = "All notifications marked as read." });
        }
    }
}