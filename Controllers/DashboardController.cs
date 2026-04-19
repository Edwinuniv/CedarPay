using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class DashboardController : BaseController
    {
        private readonly IUserRepository _userRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAgentRepository _agentRepository;
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardController(
            IUserRepository userRepository,
            IWalletRepository walletRepository,
            IBeneficiaryRepository beneficiaryRepository,
            IAccountRepository accountRepository,
            IAgentRepository agentRepository,
            IAgentApplicationRepository applicationRepository,
            UserManager<User> userManager,
            ApplicationDbContext context)
            : base(userManager, userRepository)
        {
            _userRepository = userRepository;
            _walletRepository = walletRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _accountRepository = accountRepository;
            _agentRepository = agentRepository;
            _applicationRepository = applicationRepository;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

            // Get admin statistics if user is admin
            if (User.IsInRole(Roles.Admin))
            {
                var users = await _userRepository.GetAllAsync();
                var userList = users.ToList();
                var agents = await _agentRepository.GetApprovedAgentsAsync();
                var pendingApps = await _applicationRepository.GetPendingAsync();
                var pendingWallets = await _context.WalletRequests
                    .Where(r => r.Status == WalletRequestStatus.Pending)
                    .CountAsync();
                var txCount = await _context.Transactions.CountAsync();

                ViewBag.TotalUsers = userList.Count;
                ViewBag.TotalAgents = agents.Count();
                ViewBag.PendingAgents = pendingApps.Count();
                ViewBag.PendingWallets = pendingWallets;
                ViewBag.TotalTransactions = txCount;
                ViewBag.PendingAgentsCount = pendingApps.Count();
            }

            // Get agent statistics if user is agent
            if (User.IsInRole(Roles.Agent))
            {
                var agent = await _agentRepository.GetByUserIdAsync(userId);
                if (agent != null)
                {
                    var topUps = await _context.TopUps
                        .Where(t => t.Method == TopUpMethod.Cash)
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;

                    var commissions = await _context.Commissions
                        .Where(c => c.AgentId == agent.Id)
                        .SumAsync(c => (decimal?)c.Amount) ?? 0;

                    ViewBag.AgentStore = agent;
                    ViewBag.AgentTotalCashIn = topUps;
                    ViewBag.AgentTotalCommissions = commissions;
                }
            }

            // Profile check for regular users only (admins and agents bypass)
            if (!user.ProfileCompleted
                && !User.IsInRole(Roles.Admin)
                && !User.IsInRole(Roles.Agent))
            {
                TempData["Error"] = "Please complete your profile to continue.";
                return RedirectToAction("Profile", "Account");
            }

            var wallets = (await _walletRepository
                .GetByUserIdAsync(userId)).ToList();

            var account = await _accountRepository
                .GetDefaultAccountAsync(userId);

            var sent = await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.SenderWallet != null
                         && t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.ReceiverWallet != null
                         && t.ReceiverWallet.UserId == userId
                         && (t.SenderWallet == null
                             || t.SenderWallet.UserId != userId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var allTransactions = new List<RecentTransactionViewModel>();

            foreach (var t in sent)
            {
                string name;
                string? pic = null;

                if (t.Type == TransactionType.MobileTransfer)
                {
                    name = !string.IsNullOrEmpty(t.ReceiverName)
                        ? t.ReceiverName
                        : !string.IsNullOrEmpty(t.ReceiverPhoneNumber)
                            ? t.ReceiverPhoneNumber
                            : "Mobile Transfer";
                }
                else
                {
                    name = t.ReceiverWallet?.User != null
                        ? $"{t.ReceiverWallet.User.FirstName} " +
                          $"{t.ReceiverWallet.User.LastName}"
                        : "Unknown";
                    pic = t.ReceiverWallet?.User?.ProfilePictureUrl;
                }

                allTransactions.Add(new RecentTransactionViewModel
                {
                    Id = t.Id,
                    SerialNumber = t.SerialNumber ?? "",
                    ReceiverName = name,
                    ReceiverInitials = GetInitials(name),
                    ReceiverPictureUrl = pic,
                    Amount = t.Amount,
                    CurrencySymbol = t.SenderCurrency?.Symbol ?? "$",
                    Status = t.Status.ToString(),
                    Type = FormatType(t.Type),
                    CreatedAt = t.CreatedAt,
                    IsSent = true
                });
            }

            foreach (var t in received)
            {
                string name = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "Unknown Sender";
                string? pic = t.SenderWallet?.User?.ProfilePictureUrl;

                allTransactions.Add(new RecentTransactionViewModel
                {
                    Id = t.Id,
                    SerialNumber = t.SerialNumber ?? "",
                    ReceiverName = name,
                    ReceiverInitials = GetInitials(name),
                    ReceiverPictureUrl = pic,
                    Amount = t.ConvertedAmount > 0
                        ? t.ConvertedAmount : t.Amount,
                    CurrencySymbol = t.ReceiverCurrency?.Symbol ?? "$",
                    Status = t.Status.ToString(),
                    Type = FormatType(t.Type),
                    CreatedAt = t.CreatedAt,
                    IsSent = false
                });
            }

            var recentTransactions = allTransactions
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToList();

            var thisMonth = DateTime.Now.Month;
            var thisYear = DateTime.Now.Year;

            var sentThisMonth = sent.Count(
                t => t.CreatedAt.Month == thisMonth
                  && t.CreatedAt.Year == thisYear);
            var receivedThisMonth = received.Count(
                t => t.CreatedAt.Month == thisMonth
                  && t.CreatedAt.Year == thisYear);

            var totalSent = sent
                .Where(t => t.Status == TransactionStatus.Completed)
                .Sum(t => t.Amount);
            var totalReceived = received
                .Where(t => t.Status == TransactionStatus.Completed)
                .Sum(t => t.ConvertedAmount > 0
                    ? t.ConvertedAmount : t.Amount);

            var defaultWallet = wallets.FirstOrDefault(w => w.IsDefault)
                ?? wallets.FirstOrDefault();

            var walletVMs = wallets.Select(w => new WalletSummaryViewModel
            {
                Id = w.Id,
                CurrencyCode = w.Currency?.Code ?? "",
                CurrencySymbol = w.Currency?.Symbol ?? "$",
                CurrencyName = w.Currency?.Name ?? "",
                Balance = w.Balance,
                IsDefault = w.IsDefault,
                SerialNumber = w.SerialNumber ?? ""
            }).ToList();

            var beneficiaries = (await _beneficiaryRepository
                .GetByUserIdAsync(userId)).ToList();

            var vm = new DashboardViewModel
            {
                FullName = $"{user.FirstName} {user.LastName}",
                AccountSerial = account?.SerialNumber ?? "",
                TotalBalance = defaultWallet?.Balance ?? 0,
                CurrencySymbol = defaultWallet?.Currency?.Symbol ?? "$",
                Wallets = walletVMs,
                RecentTransactions = recentTransactions,
                TotalSent = totalSent,
                TotalReceived = totalReceived,
                SentThisMonth = sentThisMonth,
                ReceivedThisMonth = receivedThisMonth,
                ActiveBeneficiaries = beneficiaries.Count,
                BeneficiaryCountries = beneficiaries
                    .Where(b => !string.IsNullOrEmpty(b.ReceiverCountry))
                    .Select(b => b.ReceiverCountry)
                    .Distinct()
                    .Count()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultWallet(int walletId)
        {
            var userId = _userManager.GetUserId(User);
            var wallets = (await _walletRepository
                .GetByUserIdAsync(userId)).ToList();

            foreach (var w in wallets)
            {
                w.IsDefault = (w.Id == walletId);
                await _walletRepository.UpdateAsync(w);
            }

            TempData["Success"] = "Default wallet updated!";
            return RedirectToAction("Index");
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var parts = name.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2
                ? name.Substring(0, 2).ToUpper()
                : name.ToUpper();
        }

        private static string FormatType(TransactionType t)
            => t switch
            {
                TransactionType.WalletToWallet => "Wallet to Wallet",
                TransactionType.MobileTransfer => "Mobile Transfer",
                TransactionType.QRPayment => "QR Payment",
                TransactionType.PaymentLink => "Payment Link",
                _ => t.ToString()
            };
    }
}