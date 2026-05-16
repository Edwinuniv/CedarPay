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

        public AgentController(IAgentRepository agentRepository, IWalletRepository walletRepository, ITopUpRepository topUpRepository, INotificationRepository notificationRepository, UserManager<User> userManager, IUserRepository userRepository, ApplicationDbContext context, IEmailService emailService): base(userManager, userRepository)
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
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                if (User.IsInRole("admin"))
                {
                    TempData["Error"] = "Use 'Become Agent' in Admin section first.";
                    return RedirectToAction("Index", "Admin");
                }
                TempData["Error"] = "You are not registered as an agent.";
                return RedirectToAction("Apply", "AgentApplication");
            }

            Agent? agent = null;
            if (Request.Cookies.TryGetValue("SelectedAgentId", out var cookieId) && int.TryParse(cookieId, out var agentId))
                agent = agents.FirstOrDefault(a => a.Id == agentId);
            agent ??= agents.FirstOrDefault();

            var topUps = await _context.TopUps
                .Include(t => t.Wallet).ThenInclude(w => w.Currency)
                .Include(t => t.Wallet).ThenInclude(w => w.User)
                .Where(t => t.Method == TopUpMethod.Cash && t.Description != null && t.Description.Contains(agent.StoreName))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var commissions = await _context.Commissions
                .Where(c => c.AgentId == agent.Id)
                .ToListAsync();

            ViewBag.Agents = agents;
            ViewBag.Agent = agent;
            ViewBag.TotalCashIn = topUps.Sum(t => t.Amount);
            ViewBag.TotalCommissions = commissions.Sum(c => c.Amount);
            ViewBag.RecentCashIn = topUps.Take(10).ToList();

            return View("Dashboard");
        }

        public IActionResult Register() => View(new Agent());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Agent agent)
        {
            var userId = _userManager.GetUserId(User);

            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Status");

            if (!ModelState.IsValid) return View(agent);

            agent.UserId = userId;
            agent.Status = AgentStatus.Approved;
            agent.RegisteredAt = DateTime.Now;
            agent.ApprovedAt = DateTime.Now;
            agent.CommissionRate = 0.02m;

            await _agentRepository.AddAsync(agent);

            Response.Cookies.Append("SelectedAgentId", agent.Id.ToString(),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddHours(8) });

            TempData["Success"] = $"Store '{agent.StoreName}' created!";
            return RedirectToAction("Dashboard");
        }

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

            if (!ModelState.IsValid) return View(agent);

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

        public IActionResult CashIn() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashIn(string walletSerial, decimal amount, string? note)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any()) { TempData["Error"] = "Agent profile not found."; return RedirectToAction("Dashboard"); }

            var agent = agents.FirstOrDefault();
            if (agent == null) { TempData["Error"] = "Please select a store first."; return RedirectToAction("Dashboard"); }

            if (string.IsNullOrWhiteSpace(walletSerial) || amount <= 0)
            {
                ModelState.AddModelError("", "Please enter a valid wallet serial and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.SerialNumber == walletSerial && w.IsActive);

            if (wallet == null) { ModelState.AddModelError("", "Wallet not found: " + walletSerial); return View(); }

            wallet.Balance += amount;
            _context.Wallets.Update(wallet);

            var topUp = new TopUp
            {
                Amount = amount,
                Status = TopUpStatus.Completed,
                Method = TopUpMethod.Cash,
                Description = note ?? $"Cash deposit by agent {agent.StoreName}",
                WalletId = wallet.Id,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference = $"CASH-{DateTime.Now.Ticks}"
            };
            await _context.TopUps.AddAsync(topUp);
            await _context.SaveChangesAsync();

            var transaction = new Transaction
            {
                SerialNumber = GenerateSerial("CASHIN"),
                Amount = amount,
                ConvertedAmount = amount,
                FeeAmount = 0,
                FeeWaived = true,
                ExchangeRateUsed = 1,
                Description = note ?? $"Cash deposit by agent {agent.StoreName}",
                Status = TransactionStatus.Completed,
                Type = TransactionType.WalletToWallet,
                CreatedAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                SenderWalletId = wallet.Id,
                ReceiverWalletId = wallet.Id,
                SenderCurrencyId = wallet.CurrencyId,
                ReceiverCurrencyId = wallet.CurrencyId
            };
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            var commissionAmount = amount * agent.CommissionRate;
            if (commissionAmount > 0)
            {
                await _context.Commissions.AddAsync(new Commission
                {
                    AgentId = agent.Id,
                    TransactionId = transaction.Id,
                    Amount = commissionAmount,
                    Percentage = agent.CommissionRate,
                    IsPaid = false,
                    EarnedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

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
                        $"Transaction Reference: {transaction.SerialNumber}");
                });
            }

            TempData["Success"] = $"Cash deposit of {wallet.Currency?.Symbol}{amount:N2} added to {walletSerial}! Commission earned: {wallet.Currency?.Symbol}{commissionAmount:N2}";
            return RedirectToAction("CashHistory");
        }

        public IActionResult CashOut() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CashOut(string walletSerial, decimal amount, string? note)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any()) { TempData["Error"] = "Agent profile not found."; return RedirectToAction("Dashboard"); }

            var agent = agents.FirstOrDefault();
            if (agent == null) { TempData["Error"] = "Please select a store first."; return RedirectToAction("Dashboard"); }

            if (string.IsNullOrWhiteSpace(walletSerial) || amount <= 0)
            {
                ModelState.AddModelError("", "Please enter a valid wallet serial and amount.");
                return View();
            }

            var wallet = await _context.Wallets
                .Include(w => w.Currency)
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.SerialNumber == walletSerial && w.IsActive);

            if (wallet == null) { ModelState.AddModelError("", "Wallet not found."); return View(); }

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
                        "If you did not authorize this transaction, please contact support immediately.");
                });
            }

            TempData["Success"] = $"Withdrawal of {wallet.Currency?.Symbol}{amount:N2} processed!";
            return RedirectToAction("CashOut");
        }

        public async Task<IActionResult> Commissions()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any()) { TempData["Error"] = "Agent profile not found."; return RedirectToAction("Dashboard"); }

            Agent? agent = null;
            if (Request.Cookies.TryGetValue("SelectedAgentId", out var cookieId) && int.TryParse(cookieId, out var agentId))
                agent = agents.FirstOrDefault(a => a.Id == agentId);
            agent ??= agents.FirstOrDefault();

            if (agent == null) { TempData["Error"] = "Please select a store first."; return RedirectToAction("Dashboard"); }

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

        public async Task<IActionResult> SetLocation(int? id)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any())
            {
                if (User.IsInRole("admin")) { TempData["Error"] = "Use 'Become Agent' first to create a store."; return RedirectToAction("Index", "Admin"); }
                TempData["Error"] = "No agent store found.";
                return RedirectToAction("Dashboard");
            }

            Agent? agent = id.HasValue ? agents.FirstOrDefault(a => a.Id == id.Value) ?? agents.First() : agents.First();

            ViewBag.AllStores = agents;
            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLocation(int id, double latitude, double longitude)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();
            var agent = agents.FirstOrDefault(a => a.Id == id);

            if (agent == null) { TempData["Error"] = "Store not found."; return RedirectToAction("Dashboard"); }

            await _agentRepository.UpdateLocationAsync(id, latitude, longitude);
            TempData["Success"] = $"Location saved for '{agent.StoreName}'!";
            return RedirectToAction("SetLocation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStore(int id, string StoreName, string AgentName, string PhoneNumber, string Email, string WorkingHours, string Street, string BuildingNumber, string City, string Region, string PostalCode, string Country, string Description, double latitude, double longitude)
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();
            var agent = agents.FirstOrDefault(a => a.Id == id);

            if (agent == null) { TempData["Error"] = "Store not found."; return RedirectToAction("Dashboard"); }

            if (!string.IsNullOrWhiteSpace(StoreName)) agent.StoreName = StoreName.Trim();
            if (!string.IsNullOrWhiteSpace(AgentName)) agent.AgentName = AgentName.Trim();
            if (!string.IsNullOrWhiteSpace(PhoneNumber)) agent.PhoneNumber = PhoneNumber.Trim();
            if (!string.IsNullOrWhiteSpace(Email)) agent.Email = Email.Trim();

            agent.WorkingHours = WorkingHours?.Trim();
            agent.Street = Street?.Trim();
            agent.BuildingNumber = BuildingNumber?.Trim();
            agent.City = City?.Trim();
            agent.Region = Region?.Trim();
            agent.PostalCode = PostalCode?.Trim();
            agent.Country = Country?.Trim();
            agent.Description = Description?.Trim();

            if (latitude != 0 && longitude != 0)
            {
                agent.Latitude = latitude;
                agent.Longitude = longitude;
            }

            await _agentRepository.UpdateAsync(agent);
            TempData["Success"] = $"Store '{agent.StoreName}' has been successfully updated!";
            return RedirectToAction("SetLocation", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SelectStore(int agentId)
        {
            Response.Cookies.Append("SelectedAgentId", agentId.ToString(),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddHours(8) });
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> CashHistory()
        {
            var userId = _userManager.GetUserId(User);
            var agents = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

            if (!agents.Any()) { TempData["Error"] = "No agent profile found."; return RedirectToAction("Dashboard"); }

            var agent = agents.FirstOrDefault();

            var cashTransactions = await _context.TopUps
                .Include(t => t.Wallet).ThenInclude(w => w.User)
                .Include(t => t.Wallet).ThenInclude(w => w.Currency)
                .Where(t => t.Method == TopUpMethod.Cash && t.Description != null && t.Description.Contains(agent.StoreName))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.TotalCashIn = cashTransactions.Sum(t => t.Amount);
            ViewBag.TotalCommissions = cashTransactions.Sum(t => t.Amount * agent.CommissionRate);
            ViewBag.TransactionCount = cashTransactions.Count;
            ViewBag.CommissionRate = agent.CommissionRate * 100;
            ViewBag.Agent = agent;

            return View(cashTransactions);
        }

        private string GenerateSerial(string prefix)
        {
            var year = DateTime.Now.Year;
            var random = new Random().Next(10000, 99999);
            return $"{prefix}-{year}-{random}";
        }
    }
}