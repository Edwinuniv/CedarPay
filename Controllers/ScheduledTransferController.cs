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
    public class ScheduledTransferController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWalletRepository _walletRepository;

        public ScheduledTransferController(ApplicationDbContext context, IWalletRepository walletRepository, UserManager<User> userManager, IUserRepository userRepository): base(userManager, userRepository)
        {
            _context = context;
            _walletRepository = walletRepository;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var scheduled = await _context.ScheduledTransfers
                .Include(s => s.SenderWallet).ThenInclude(w => w.Currency)
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.NextRunAt)
                .ToListAsync();

            var wallets = await _walletRepository.GetByUserIdAsync(userId);
            ViewBag.Wallets = wallets.ToList();

            return View(scheduled);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int senderWalletId, string? receiverWalletSerial, string? receiverPhoneNumber, string? receiverName, decimal amount, string? description, ScheduleFrequency frequency, DateTime firstRunAt)
        {
            var userId = _userManager.GetUserId(User);

            if (amount <= 0)
            {
                TempData["Error"] = "Amount must be greater than zero.";
                return RedirectToAction("Index");
            }

            var wallet = await _walletRepository.GetByIdAsync(senderWalletId);
            if (wallet == null || wallet.UserId != userId)
            {
                TempData["Error"] = "Invalid wallet selected.";
                return RedirectToAction("Index");
            }

            var scheduled = new ScheduledTransfer
            {
                UserId = userId,
                SenderWalletId = senderWalletId,
                ReceiverWalletSerial = receiverWalletSerial?.Trim(),
                ReceiverPhoneNumber = receiverPhoneNumber?.Trim(),
                ReceiverName = receiverName?.Trim(),
                Amount = amount,
                Description = description?.Trim(),
                Frequency = frequency,
                NextRunAt = firstRunAt,
                IsActive = true,
                ExecutionCount = 0,
                CreatedAt = DateTime.Now
            };

            await _context.ScheduledTransfers.AddAsync(scheduled);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Scheduled transfer created!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.ScheduledTransfers.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (transfer == null) return NotFound();

            transfer.IsActive = !transfer.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = transfer.IsActive ? "Scheduled transfer resumed." : "Scheduled transfer paused.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.ScheduledTransfers.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (transfer == null) return NotFound();

            _context.ScheduledTransfers.Remove(transfer);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Scheduled transfer deleted.";
            return RedirectToAction("Index");
        }
    }
}