using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = "agent,admin")]
    public class AgentController : BaseController
    {
        private readonly IAgentRepository _agentRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ITopUpRepository _topUpRepository;
        private readonly INotificationRepository
            _notificationRepository;
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

        // Agent Dashboard — the main page
        public async Task<IActionResult> Index()
        {
            return await Dashboard();
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository
                .GetByUserIdAsync(userId)).ToList();
            var agent = agents.FirstOrDefault();

            var topUps = await _context.TopUps
                .Where(t => t.Method == TopUpMethod.Cash)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .ToListAsync();

            var commissions = agent != null
                ? await _context.Commissions
                    .Where(c => c.AgentId == agent.Id)
                    .ToListAsync()
                : new List<Commission>();

            ViewBag.Agent = agent;
            ViewBag.TotalCashIn = topUps.Sum(t => t.Amount);
            ViewBag.TotalCommissions =
                commissions.Sum(c => c.Amount);
            ViewBag.RecentCashIn = topUps;

            return View("Dashboard");
        }

        // Cash In
        public IActionResult CashIn() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashIn(
            string walletSerial,
            decimal amount,
            string? note)
        {
            if (string.IsNullOrWhiteSpace(walletSerial)
                || amount <= 0)
            {
                ModelState.AddModelError("",
                    "Please enter a valid wallet serial " +
                    "and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w =>
                    w.SerialNumber == walletSerial &&
                    w.IsActive);

            if (wallet == null)
            {
                ModelState.AddModelError("",
                    "Wallet not found: " + walletSerial);
                return View();
            }

            wallet.Balance += amount;
            _context.Wallets.Update(wallet);

            await _context.TopUps.AddAsync(new TopUp
            {
                Amount = amount,
                Status = TopUpStatus.Completed,
                Method = TopUpMethod.Cash,
                Description = note ?? "Cash deposit by agent",
                WalletId = wallet.Id,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference =
                    $"CASH-{DateTime.Now.Ticks}"
            });

            await _context.Notifications.AddAsync(
                new Notification
                {
                    Title = "Cash Deposit Received",
                    Message = $"An agent deposited " +
                        $"{wallet.Currency?.Symbol}" +
                        $"{amount:N2} into your wallet.",
                    Type = NotificationType.TopUpCompleted,
                    UserId = wallet.UserId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Cash deposit of " +
                $"{wallet.Currency?.Symbol}{amount:N2} " +
                $"added to {walletSerial}!";
            return RedirectToAction("CashIn");
        }

        // Cash Out
        public IActionResult CashOut() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashOut(
            string walletSerial,
            decimal amount,
            string? note)
        {
            if (string.IsNullOrWhiteSpace(walletSerial)
                || amount <= 0)
            {
                ModelState.AddModelError("",
                    "Please enter a valid wallet serial " +
                    "and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .FirstOrDefaultAsync(w =>
                    w.SerialNumber == walletSerial &&
                    w.IsActive);

            if (wallet == null)
            {
                ModelState.AddModelError("",
                    "Wallet not found.");
                return View();
            }

            if (wallet.Balance < amount)
            {
                ModelState.AddModelError("",
                    $"Insufficient balance. " +
                    $"Available: {wallet.Currency?.Symbol}" +
                    $"{wallet.Balance:N2}");
                return View();
            }

            wallet.Balance -= amount;
            _context.Wallets.Update(wallet);

            await _context.Notifications.AddAsync(
                new Notification
                {
                    Title = "Cash Withdrawal",
                    Message = $"You withdrew " +
                        $"{wallet.Currency?.Symbol}" +
                        $"{amount:N2} via an agent.",
                    Type = NotificationType.General,
                    UserId = wallet.UserId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Withdrawal of " +
                $"{wallet.Currency?.Symbol}{amount:N2} " +
                $"processed!";
            return RedirectToAction("CashOut");
        }

        // Commissions
        public async Task<IActionResult> Commissions()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository
                .GetByUserIdAsync(userId)).ToList();
            var agentIds = agents.Select(a => a.Id).ToList();

            var commissions = await _context.Commissions
                .Include(c => c.Transaction)
                .Where(c => agentIds.Contains(c.AgentId))
                .OrderByDescending(c => c.EarnedAt)
                .ToListAsync();

            ViewBag.TotalEarned = commissions.Sum(c => c.Amount);
            ViewBag.TotalPaid = commissions
                .Where(c => c.IsPaid).Sum(c => c.Amount);

            return View(commissions);
        }

        // Set Location
        public async Task<IActionResult> SetLocation()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository
                .GetByUserIdAsync(userId)).ToList();
            var agent = agents.FirstOrDefault();

            if (agent == null)
            {
                // Admin without agent record
                TempData["Error"] =
                    "No agent store found. " +
                    "Use 'Become Agent' in Admin section first.";
                return RedirectToAction("Dashboard",
                    "Admin");
            }

            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLocation(
            int id, double latitude, double longitude)
        {
            var agent = await _agentRepository
                .GetByIdAsync(id);
            if (agent == null) return NotFound();

            agent.Latitude = latitude;
            agent.Longitude = longitude;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] =
                "Location saved! " +
                "Your store now appears on the agent map.";
            return RedirectToAction("Dashboard");
        }
    }
}