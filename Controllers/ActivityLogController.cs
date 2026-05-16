using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class ActivityLogController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogController(ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository): base(userManager, userRepository)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var logs = await _context.ActivityLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(logs);
        }

        public static async Task LogAsync(ApplicationDbContext context, string userId, ActivityType action, string? details = null, string? ipAddress = null, string? userAgent = null)
        {
            await context.ActivityLogs.AddAsync(new ActivityLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }
}