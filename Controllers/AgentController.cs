using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = "Agent,Admin")]
    public class AgentController : BaseController
    {
        private readonly IAgentRepository _agentRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ITopUpRepository _topUpRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public AgentController(
            IAgentRepository agentRepository,
            IWalletRepository walletRepository,
            ITopUpRepository topUpRepository,
            INotificationRepository notificationRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            ApplicationDbContext context)
            : base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
            _walletRepository = walletRepository;
            _topUpRepository = topUpRepository;
            _notificationRepository = notificationRepository;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var agent = await _agentRepository.GetByUserIdAsync(userId);

            if (agent == null)
            {
                if (User.IsInRole("Admin"))
                {
                    TempData["Error"] = "You are not registered as an agent. Use 'Become Agent' in Admin section first.";
                    return RedirectToAction("Index", "Admin");
                }
                TempData["Error"] = "You are not registered as an agent. Please apply first.";
                return RedirectToAction("Apply", "AgentApplication");
            }

            var topUps = await _context.TopUps
                .Include(t => t.Wallet)
                    .ThenInclude(w => w.User)
                .Where(t => t.Method == TopUpMethod.Cash)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            var commissions = await _context.Commissions
                .Include(c => c.Transaction)
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.EarnedAt)
                .ToListAsync();

            ViewBag.Agent = agent;
            ViewBag.TotalCashIn = topUps.Sum(t => t.Amount);
            ViewBag.TotalCommissions = commissions.Sum(c => c.Amount);
            ViewBag.RecentCashIn = topUps;

            return View("Dashboard");
        }

        public IActionResult CashIn() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashIn(
            string walletSerial,
            decimal amount,
            string? note)
        {
            var userId = _userManager.GetUserId(User);
            var agent = await _agentRepository.GetByUserIdAsync(userId);

            if (agent == null)
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            if (string.IsNullOrWhiteSpace(walletSerial) || amount <= 0)
            {
                ModelState.AddModelError("", "Please enter a valid wallet serial and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.SerialNumber == walletSerial && w.IsActive);

            if (wallet == null)
            {
                ModelState.AddModelError("", "Wallet not found: " + walletSerial);
                return View();
            }

            wallet.Balance += amount;
            _context.Wallets.Update(wallet);

            await _context.TopUps.AddAsync(new TopUp
            {
                Amount = amount,
                Status = TopUpStatus.Completed,
                Method = TopUpMethod.Cash,
                Description = note ?? $"Cash deposit by agent {agent.StoreName}",
                WalletId = wallet.Id,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference = $"CASH-{DateTime.Now.Ticks}"
            });

            await _context.Notifications.AddAsync(new Notification
            {
                Title = "Cash Deposit Received",
                Message = $"Agent {agent.StoreName} deposited {wallet.Currency?.Symbol}{amount:N2} into your wallet.",
                Type = NotificationType.TopUpCompleted,
                UserId = wallet.UserId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Cash deposit of {wallet.Currency?.Symbol}{amount:N2} added to {walletSerial}!";
            return RedirectToAction("CashIn");
        }

        public IActionResult CashOut() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashOut(
            string walletSerial,
            decimal amount,
            string? note)
        {
            var userId = _userManager.GetUserId(User);
            var agent = await _agentRepository.GetByUserIdAsync(userId);

            if (agent == null)
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            if (string.IsNullOrWhiteSpace(walletSerial) || amount <= 0)
            {
                ModelState.AddModelError("", "Please enter a valid wallet serial and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .FirstOrDefaultAsync(w => w.SerialNumber == walletSerial && w.IsActive);

            if (wallet == null)
            {
                ModelState.AddModelError("", "Wallet not found.");
                return View();
            }

            if (wallet.Balance < amount)
            {
                ModelState.AddModelError("", $"Insufficient balance. Available: {wallet.Currency?.Symbol}{wallet.Balance:N2}");
                return View();
            }

            wallet.Balance -= amount;
            _context.Wallets.Update(wallet);

            await _context.Notifications.AddAsync(new Notification
            {
                Title = "Cash Withdrawal",
                Message = $"You withdrew {wallet.Currency?.Symbol}{amount:N2} via agent {agent.StoreName}.",
                Type = NotificationType.General,
                UserId = wallet.UserId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Withdrawal of {wallet.Currency?.Symbol}{amount:N2} processed!";
            return RedirectToAction("CashOut");
        }

        public async Task<IActionResult> Commissions()
        {
            var userId = _userManager.GetUserId(User);
            var agent = await _agentRepository.GetByUserIdAsync(userId);

            if (agent == null)
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            var commissions = await _context.Commissions
                .Include(c => c.Transaction)
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.EarnedAt)
                .ToListAsync();

            ViewBag.TotalEarned = commissions.Sum(c => c.Amount);
            ViewBag.TotalPaid = commissions.Where(c => c.IsPaid).Sum(c => c.Amount);

            return View(commissions);
        }

        public async Task<IActionResult> SetLocation()
        {
            var userId = _userManager.GetUserId(User);
            var agent = await _agentRepository.GetByUserIdAsync(userId);

            if (agent == null)
            {
                if (User.IsInRole("Admin"))
                {
                    TempData["Error"] = "No agent store found. Use 'Become Agent' in Admin section first.";
                    return RedirectToAction("Index", "Admin");
                }
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLocation(int id, double latitude, double longitude)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null) return NotFound();

            agent.Latitude = latitude;
            agent.Longitude = longitude;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] = "Location saved! Your store now appears on the agent map.";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> EditLocation(int id)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (agent.UserId != userId && !User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            return View(agent);
        }

        [HttpPost]
        public async Task<IActionResult> EditLocation(int id, double latitude, double longitude)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (agent.UserId != userId && !User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            agent.Latitude = latitude;
            agent.Longitude = longitude;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] = "Location updated successfully!";
            return RedirectToAction("Dashboard");
        }
    }
}