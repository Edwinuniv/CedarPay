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
    public class ReferralController : BaseController
    {
        private readonly ApplicationDbContext _context;

        public ReferralController(ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository): base(userManager, userRepository)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var code = await _context.ReferralCodes
                .Include(r => r.Uses).ThenInclude(u => u.ReferredUser)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.IsActive);

            if (code == null)
            {
                code = new ReferralCode
                {
                    UserId = userId,
                    Code = GenerateCode(),
                    IsActive = true,
                    UsageCount = 0,
                    CreatedAt = DateTime.Now
                };
                await _context.ReferralCodes.AddAsync(code);
                await _context.SaveChangesAsync();
            }

            return View(code);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Regenerate()
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _context.ReferralCodes.FirstOrDefaultAsync(r => r.UserId == userId && r.IsActive);
            if (existing != null)
            {
                existing.IsActive = false;
                _context.ReferralCodes.Update(existing);
            }

            var newCode = new ReferralCode
            {
                UserId = userId,
                Code = GenerateCode(),
                IsActive = true,
                UsageCount = 0,
                CreatedAt = DateTime.Now
            };
            await _context.ReferralCodes.AddAsync(newCode);
            await _context.SaveChangesAsync();

            TempData["Success"] = "New referral code generated!";
            return RedirectToAction("Index");
        }

        private static string GenerateCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = new Random();
            return new string(Enumerable.Repeat(chars, 8).Select(s => s[rng.Next(s.Length)]).ToArray());
        }
    }
}