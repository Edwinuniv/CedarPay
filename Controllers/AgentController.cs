using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = "Agent,Admin")]
    public class AgentController : BaseController
    {
        private readonly IAgentRepository _agentRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ITopUpRepository _topUpRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AgentController(
            IAgentRepository agentRepository,
            IWalletRepository walletRepository,
            ITopUpRepository topUpRepository,
            INotificationRepository notificationRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            ApplicationDbContext context,
            IEmailService emailService)
            : base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
            _walletRepository = walletRepository;
            _topUpRepository = topUpRepository;
            _notificationRepository = notificationRepository;
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository
                .GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                if (User.IsInRole("admin"))
                {
                    TempData["Error"] =
                        "Use 'Become Agent' in Admin section first.";
                    return RedirectToAction("Index", "Admin");
                }
                TempData["Error"] =
                    "You are not registered as an agent.";
                return RedirectToAction("Apply", "AgentApplication");
            }

            // Get selected agent from cookie, or default to first
            Agent? agent = null;
            if (Request.Cookies.TryGetValue(
                "SelectedAgentId", out var cookieId) &&
                int.TryParse(cookieId, out var agentId))
            {
                agent = agents.FirstOrDefault(a => a.Id == agentId);
            }
            agent ??= agents.FirstOrDefault();

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

            ViewBag.Agents = agents;
            ViewBag.Agent = agent;
            ViewBag.TotalCashIn = topUps.Sum(t => t.Amount);
            ViewBag.TotalCommissions = commissions.Sum(c => c.Amount);
            ViewBag.RecentCashIn = topUps;

            return View("Dashboard");
        }

        // Create new store (agents can have multiple)
        public IActionResult Register()
        {
            return View(new Agent());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Agent agent)
        {
            var userId = _userManager.GetUserId(User);

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Status");

            if (!ModelState.IsValid)
                return View(agent);

            agent.UserId = userId;
            agent.Status = AgentStatus.Approved;
            agent.RegisteredAt = DateTime.Now;
            agent.ApprovedAt = DateTime.Now;
            agent.CommissionRate = 0.02m;

            await _agentRepository.AddAsync(agent);

            // Set cookie to this new agent
            Response.Cookies.Append("SelectedAgentId",
                agent.Id.ToString(),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8)
                });

            TempData["Success"] = $"Store '{agent.StoreName}' created!";
            return RedirectToAction("Dashboard");
        }

        // Edit store
        public async Task<IActionResult> EditStore(int id)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null) return NotFound();
            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStore(Agent agent)
        {
            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (!ModelState.IsValid)
                return View(agent);

            var existing = await _agentRepository.GetByIdAsync(agent.Id);
            if (existing == null) return NotFound();

            existing.StoreName = agent.StoreName;
            existing.AgentName = agent.AgentName;
            existing.PhoneNumber = agent.PhoneNumber;
            existing.Email = agent.Email;
            existing.Street = agent.Street;
            existing.City = agent.City;
            existing.Region = agent.Region;
            existing.Country = agent.Country;
            existing.WorkingHours = agent.WorkingHours;
            existing.Description = agent.Description;

            await _agentRepository.UpdateAsync(existing);

            TempData["Success"] = "Store updated!";
            return RedirectToAction("Dashboard");
        }

        // Delete store
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStore(int id)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null) return NotFound();

            await _agentRepository.DeleteAsync(id);
            TempData["Success"] = $"Store '{agent.StoreName}' deleted.";
            return RedirectToAction("Dashboard");
        }

        public IActionResult CashIn()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashIn(
            string walletSerial,
            decimal amount,
            string? note)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            var agent = agents.FirstOrDefault();

            if (agent == null)
            {
                TempData["Error"] = "Please select a store first.";
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

            // Send email notification for cash deposit
            var walletUser = wallet.User;
            if (walletUser?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        walletUser.Email,
                        $"{walletUser.FirstName} {walletUser.LastName}",
                        "Cash Deposit Received",
                        $"Agent {agent.StoreName} has deposited {wallet.Currency?.Symbol}{amount:N2} into your wallet ({walletSerial}).\n\n" +
                        $"Your new balance is: {wallet.Currency?.Symbol}{wallet.Balance:N2}\n\n" +
                        $"Transaction Reference: CASH-{DateTime.Now.Ticks}");
                });
            }

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
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            var agent = agents.FirstOrDefault();

            if (agent == null)
            {
                TempData["Error"] = "Please select a store first.";
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

            // Send email notification for cash withdrawal
            var walletUser = wallet.User;
            if (walletUser?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        walletUser.Email,
                        $"{walletUser.FirstName} {walletUser.LastName}",
                        "Cash Withdrawal Processed",
                        $"Agent {agent.StoreName} has processed a withdrawal of {wallet.Currency?.Symbol}{amount:N2} from your wallet ({walletSerial}).\n\n" +
                        $"Your new balance is: {wallet.Currency?.Symbol}{wallet.Balance:N2}\n\n" +
                        $"If you did not authorize this transaction, please contact support immediately.");
                });
            }

            TempData["Success"] = $"Withdrawal of {wallet.Currency?.Symbol}{amount:N2} processed!";
            return RedirectToAction("CashOut");
        }

        public async Task<IActionResult> Commissions()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                TempData["Error"] = "Agent profile not found.";
                return RedirectToAction("Dashboard");
            }

            var agent = agents.FirstOrDefault();

            if (agent == null)
            {
                TempData["Error"] = "Please select a store first.";
                return RedirectToAction("Dashboard");
            }

            var commissions = await _context.Commissions
                .Include(c => c.Transaction)
                .Where(c => c.AgentId == agent.Id)
                .OrderByDescending(c => c.EarnedAt)
                .ToListAsync();

            ViewBag.TotalEarned = commissions.Sum(c => c.Amount);
            ViewBag.TotalPaid = commissions.Where(c => c.IsPaid).Sum(c => c.Amount);
            ViewBag.Agent = agent;

            return View(commissions);
        }

        // ✅ FIX: Takes int? id from the URL route so each store loads correctly
        public async Task<IActionResult> SetLocation(int? id)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository
                .GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                if (User.IsInRole("admin"))
                {
                    TempData["Error"] =
                        "Use 'Become Agent' first to create a store.";
                    return RedirectToAction("Index", "Admin");
                }
                TempData["Error"] = "No agent store found.";
                return RedirectToAction("Dashboard");
            }

            // ✅ FIX: Load the specific store by route id, fall back to first
            Agent? agent;
            if (id.HasValue)
            {
                // Security: only allow stores that belong to this user
                agent = agents.FirstOrDefault(a => a.Id == id.Value)
                        ?? agents.First();
            }
            else
            {
                agent = agents.First();
            }

            // ✅ FIX: Pass all stores so the dropdown switcher renders
            ViewBag.AllStores = agents;

            return View(agent);
        }

        // ✅ FIX: Added latitude and longitude parameters + calls UpdateLocationAsync
        //         to save to the SPECIFIC store identified by id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLocation(int id, double latitude, double longitude)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            // Security: verify this store belongs to the current user
            var agent = agents.FirstOrDefault(a => a.Id == id);

            if (agent == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToAction("Dashboard");
            }

            // ✅ FIX: Actually save the lat/lng to the correct store
            await _agentRepository.UpdateLocationAsync(id, latitude, longitude);

            TempData["Success"] = $"Location saved for '{agent.StoreName}'!";
            return RedirectToAction("Dashboard");
        }


        // Update store name, phone, working hours from SetLocation page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStoreInfo(int id, string storeName, string phoneNumber, string workingHours)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();
            var agent = agents.FirstOrDefault(a => a.Id == id);

            if (agent == null)
            {
                TempData["Error"] = "Store not found.";
                return RedirectToAction("Dashboard");
            }

            if (!string.IsNullOrWhiteSpace(storeName))
                agent.StoreName = storeName.Trim();
            if (!string.IsNullOrWhiteSpace(phoneNumber))
                agent.PhoneNumber = phoneNumber.Trim();
            agent.WorkingHours = workingHours?.Trim();

            await _agentRepository.UpdateAsync(agent);
            TempData["Success"] = $"Store '{agent.StoreName}' info updated!";
            return RedirectToAction("SetLocation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SelectStore(int agentId)
        {
            Response.Cookies.Append("SelectedAgentId",
                agentId.ToString(),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddHours(8)
                });
            return RedirectToAction("Dashboard");
        }
    }
}