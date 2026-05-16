using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Implementations;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : BaseController
    {
        private readonly IAgentRepository _agentRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly ApplicationDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly ICurrencyExchangeService _exchangeService;
        private readonly IEmailService _emailService;

        public AdminController(IUserRepository userRepository, IAgentRepository agentRepository, ITransactionRepository transactionRepository, IReviewRepository reviewRepository, IAgentApplicationRepository applicationRepository,  UserManager<User> userManager, ApplicationDbContext context, IAccountRepository accountRepository, IWalletRepository walletRepository, ICurrencyExchangeService currencyExchangeService, IEmailService emailService) : base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
            _transactionRepository = transactionRepository;
            _reviewRepository = reviewRepository;
            _applicationRepository = applicationRepository;
            _context = context;
            _accountRepository = accountRepository;
            _walletRepository = walletRepository;
            _exchangeService = currencyExchangeService;
            _emailService = emailService;
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Index()
        {
            var users = await _userRepository.GetAllAsync();
            var userList = users.ToList();

            var agents = await _agentRepository
                .GetApprovedAgentsAsync();

            var pendingApps = await _applicationRepository
                .GetPendingAsync();

            var pendingWallets = await _context.WalletRequests
                .Where(r => r.Status ==
                    WalletRequestStatus.Pending)
                .CountAsync();

            var txCount = await _context.Transactions.CountAsync();

            var avg = await _reviewRepository
                .GetAverageAppRatingAsync();

            ViewBag.TotalUsers = userList.Count;
            ViewBag.TotalAgents = agents.Count();
            ViewBag.PendingAgents = pendingApps.Count();
            ViewBag.PendingWallets = pendingWallets;
            ViewBag.TotalTransactions = txCount;
            ViewBag.AverageRating = avg;
            ViewBag.PendingAgentsCount = pendingApps.Count();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ApproveAgent(int id)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null)
            {
                return NotFound();
            }
            agent.Status = AgentStatus.Approved;
            agent.ApprovedAt = DateTime.Now;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] = "Agent approved!";
            return RedirectToAction("PendingAgents");
        }

        [HttpPost]
        public async Task<IActionResult> RejectAgent(int id)
        {
            var agent = await _agentRepository.GetByIdAsync(id);
            if (agent == null)
            {
                return NotFound();
            }
            agent.Status = AgentStatus.Rejected;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] = "Agent rejected!";
            return RedirectToAction("PendingAgents");
        }

        public async Task<IActionResult> PendingAgents()
        {
            var agents = await _agentRepository.GetPendingAgentsAsync();
            return View(agents);
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userRepository.GetAllAsync();
            var userList = users.ToList();

            var roles = new Dictionary<string, List<string>>();
            foreach (var u in userList)
            {
                var r = await _userManager.GetRolesAsync(u);
                roles[u.Id] = r.ToList();
            }

            ViewBag.UserRoles = roles;
            ViewBag.TotalUsers = userList.Count;

            return View(userList);
        }

        public async Task<IActionResult> AgentApplications()
        {
            var applications = await _applicationRepository.GetPendingAsync();
            return View(applications.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> ApproveApplication(int id)
        {
            var application = await _applicationRepository.GetByIdAsync(id);
            if (application == null) return NotFound();

            application.Status = ApplicationStatus.Approved;
            application.ReviewedAt = DateTime.Now;
            await _applicationRepository.UpdateAsync(application);

            var user = await _userManager.FindByIdAsync(application.UserId);
            if (user != null && !await _userManager.IsInRoleAsync(user, Roles.Agent))
            {
                await _userManager.AddToRoleAsync(user, Roles.Agent);
            }

            var agent = new Agent
            {
                AgentName = application.AgentName,
                StoreName = application.StoreName,
                PhoneNumber = application.PhoneNumber,
                Email = application.Email,
                Street = application.Street,
                City = application.City,
                Region = application.Region,
                Country = application.Country,
                WorkingHours = application.WorkingHours,
                Description = application.Description,
                Status = AgentStatus.Approved,
                UserId = application.UserId,
                RegisteredAt = application.SubmittedAt,
                ApprovedAt = DateTime.Now,
                CommissionRate = 0.02m
            };
            await _agentRepository.AddAsync(agent);

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Agent Application Approved!",
                        $"Congratulations! Your agent application for '{application.StoreName}' has been approved. You can now access your agent dashboard and start serving customers.");
                });
            }

            TempData["Success"] = $"Agent {application.StoreName} approved!";
            return RedirectToAction("AgentApplications");
        }

        [HttpPost]
        public async Task<IActionResult> RejectApplication(int id, string reason)
        {
            var application = await _applicationRepository.GetByIdAsync(id);
            if (application == null) return NotFound();

            application.Status = ApplicationStatus.Rejected;
            application.RejectionReason = reason;
            application.ReviewedAt = DateTime.Now;
            await _applicationRepository.UpdateAsync(application);

            var user = await _userManager.FindByIdAsync(application.UserId);

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Agent Application Update",
                        $"Your agent application for '{application.StoreName}' has been reviewed. Status: Rejected.\nReason: {reason}\n\nYou can reapply after addressing the issues mentioned.");
                });
            }

            TempData["Success"] = "Application rejected.";
            return RedirectToAction("AgentApplications");
        }

        [HttpPost]
        public async Task<IActionResult> PromoteToAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            if (!await _userManager.IsInRoleAsync(user, Roles.Admin))
            {
                await _userManager.AddToRoleAsync(user, Roles.Admin);
                TempData["Success"] = $"{user.FirstName} {user.LastName} promoted to Admin!";
            }
            else
            {
                TempData["Error"] = "User is already an Admin.";
            }

            return RedirectToAction("Users");
        }

        public async Task<IActionResult> KYCReview()
        {
            var pending = await _context.KYCDocuments
                .Include(k => k.User)
                .Where(k => k.Status == KYCStatus.Pending)
                .ToListAsync();
            return View(pending);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveKYC(int id)
        {
            var kyc = await _context.KYCDocuments.FindAsync(id);
            if (kyc == null)
            {
                return NotFound();
            }
            kyc.Status = KYCStatus.Approved;
            kyc.ReviewedAt = DateTime.Now;
            _context.KYCDocuments.Update(kyc);

            var user = await _userManager.FindByIdAsync(kyc.UserId);
            if (user != null)
            {
                user.IsVerified = true;
                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "KYC Approved!",
                        "Congratulations! Your identity has been verified. You now have full access to all CedarPay features including higher transfer limits.");
                });
            }

            TempData["Success"] = "KYC approved!";
            return RedirectToAction("KYCReview");
        }

        [HttpPost]
        public async Task<IActionResult> RejectKYC(int id, string reason)
        {
            var kyc = await _context.KYCDocuments.FindAsync(id);
            if (kyc == null) return NotFound();

            kyc.Status = KYCStatus.Rejected;
            kyc.RejectionReason = reason;
            kyc.ReviewedAt = DateTime.Now;
            _context.KYCDocuments.Update(kyc);

            var user = await _userManager.FindByIdAsync(kyc.UserId);

            await _context.SaveChangesAsync();

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "KYC Update Required",
                        $"Your identity verification has been reviewed.\nStatus: Rejected\nReason: {reason}\n\nPlease upload new documents addressing the issues mentioned.");
                });
            }

            TempData["Success"] = "KYC rejected.";
            return RedirectToAction("KYCReview");
        }

        public async Task<IActionResult> WalletRequests()
        {
            var requests = await _context.WalletRequests
                .Include(r => r.User)
                .Include(r => r.Currency)
                .Where(r => r.Status == WalletRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveWallet(int id)
        {
            var request = await _context.WalletRequests
                .Include(r => r.User)
                .Include(r => r.Currency)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }
            request.Status = WalletRequestStatus.Approved;
            request.ReviewedAt = DateTime.Now;
            _context.WalletRequests.Update(request);

            var account = await _accountRepository
                .GetDefaultAccountAsync(request.UserId);

            if (account == null)
            {
                account = new Account
                {
                    SerialNumber = GenerateAdminSerial("ACC"),
                    UserId = request.UserId,
                    IsActive = true,
                    IsDefault = true,
                    CreatedAt = DateTime.Now
                };
                await _accountRepository.AddAsync(account);
            }

            var existingWallets = await _walletRepository.GetByUserIdAsync(request.UserId);
            var isFirst = !existingWallets.Any();

            var wallet = new Wallet
            {
                SerialNumber = GenerateAdminSerial("WAL"),
                Balance = 0,
                Description = request.Description,
                IsActive = true,
                IsDefault = isFirst,
                CreatedAt = DateTime.Now,
                UserId = request.UserId,
                AccountId = account.Id,
                CurrencyId = request.CurrencyId
            };

            await _walletRepository.AddAsync(wallet);
            await _context.SaveChangesAsync();

            var user = request.User;
            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Wallet Request Approved!",
                        $"Your request for a {request.Currency?.Code} wallet has been approved. You can now start using your new wallet.");
                });
            }

            TempData["Success"] = $"Wallet approved for {request.User?.FirstName}!";
            return RedirectToAction("WalletRequests");
        }

        [HttpPost]
        public async Task<IActionResult> RejectWallet(int id, string reason)
        {
            var request = await _context.WalletRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }
            request.Status = WalletRequestStatus.Rejected;
            request.RejectionReason = reason;
            request.ReviewedAt = DateTime.Now;
            _context.WalletRequests.Update(request);
            await _context.SaveChangesAsync();

            var user = request.User;
            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Wallet Request Update",
                        $"Your wallet request for {request.Currency?.Code} has been reviewed.\nStatus: Rejected\nReason: {reason}\n\nYou can submit a new request after addressing the issues.");
                });
            }

            TempData["Success"] = "Wallet request rejected.";
            return RedirectToAction("WalletRequests");
        }

        private string GenerateAdminSerial(string prefix)
        {
            var year = DateTime.Now.Year;
            var random = new Random().Next(10000, 99999);
            return $"{prefix}-{year}-{random}";
        }

        public async Task<IActionResult> AllTransactions()
        {
            var transactions = await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .OrderByDescending(t => t.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(transactions);
        }

        public async Task<IActionResult> Reviews()
        {
            var reviews = await _reviewRepository.GetAppReviewsAsync();
            var avg = await _reviewRepository.GetAverageAppRatingAsync();
            ViewBag.AverageRating = avg;
            return View(reviews.ToList());
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReview(int id)
        {
            await _reviewRepository.DeleteAsync(id);
            TempData["Success"] = "Review deleted.";
            return RedirectToAction("Reviews");
        }

        [HttpGet]
        public async Task<IActionResult> InstantAgent()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            var existingAgent = await _agentRepository.GetByUserIdAsync(userId);

            if (existingAgent == null)
            {
                await _agentRepository.AddAsync(new Agent
                {
                    AgentName = $"{user?.FirstName} {user?.LastName}",
                    StoreName = $"{user?.FirstName}'s Store",
                    PhoneNumber = user?.PhoneNumber ?? "",
                    Email = user?.Email ?? "",
                    Status = AgentStatus.Approved,
                    UserId = userId,
                    RegisteredAt = DateTime.Now,
                    ApprovedAt = DateTime.Now,
                    CommissionRate = 0.02m
                });
            }

            if (user != null && !await _userManager.IsInRoleAsync(user, Roles.Agent))
            {
                await _userManager.AddToRoleAsync(user, Roles.Agent);
            }

            TempData["Success"] = "You are now an agent!";
            return RedirectToAction("Index", "Agent");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = user.IsActive ? "Account enabled." : "Account disabled.";
            return RedirectToAction("Users");
        }

        public async Task<IActionResult> UserDetails(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            var wallets = await _walletRepository.GetByUserIdAsync(userId);
            var kyc = await _context.KYCDocuments.FirstOrDefaultAsync(k => k.UserId == userId);
            var roles = await _userManager.GetRolesAsync(user);

            ViewBag.Wallets = wallets.ToList();
            ViewBag.KYC = kyc;
            ViewBag.Roles = roles;

            return View(user);
        }

        public async Task<IActionResult> FeeSettings()
        {
            var feePolicy = await _context.FeePolicies
                .FirstOrDefaultAsync();
            return View(feePolicy ?? new FeePolicy());
        }

        [HttpPost]
        public async Task<IActionResult> FeeSettings(FeePolicy model)
        {
            var existing = await _context.FeePolicies.FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.FeePercentage = model.FeePercentage;
                existing.FixedFee = model.FixedFee;
                existing.FreeTransactionThreshold = model.FreeTransactionThreshold;
                _context.FeePolicies.Update(existing);
            }
            else
            {
                await _context.FeePolicies.AddAsync(model);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Fee settings updated!";
            return RedirectToAction("FeeSettings");
        }

        public async Task<IActionResult> UserReport(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }
            var sent = await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Where(t => t.SenderWallet != null &&
                            t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w.User)
                .Where(t => t.ReceiverWallet != null &&
                            t.ReceiverWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var wallets = await _walletRepository
                .GetByUserIdAsync(userId);

            ViewBag.User = user;
            ViewBag.Sent = sent;
            ViewBag.Received = received;
            ViewBag.Wallets = wallets.ToList();
            ViewBag.GeneratedAt = DateTime.Now;

            return View("~/Views/Account/Report.cshtml");
        }

        public async Task<IActionResult> ExchangeRates()
        {
            var currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .ToListAsync();

            var rates = new Dictionary<string, Dictionary<string, decimal>>();
            var baseCurrencies = currencies.Select(c => c.Code).ToList();

            foreach (var fromCode in baseCurrencies)
            {
                rates[fromCode] = new Dictionary<string, decimal>();
                foreach (var toCode in baseCurrencies)
                {
                    if (fromCode == toCode)
                    {
                        rates[fromCode][toCode] = 1m;
                        continue;
                    }
                    try
                    {
                        var rate = await _exchangeService
                            .GetRateAsync(fromCode, toCode);
                        rates[fromCode][toCode] = rate;
                    }
                    catch
                    {
                        rates[fromCode][toCode] = 0m;
                    }
                }
            }

            ViewBag.Currencies = currencies;
            ViewBag.Rates = rates;
            ViewBag.LastUpdated = DateTime.Now;

            return View();
        }

        public async Task<IActionResult> EarningsReport()
        {
            var totalFees = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed
                         && !t.FeeWaived)
                .SumAsync(t => (decimal?)t.FeeAmount) ?? 0;

            var freeTransactions = await _context.Transactions
                .CountAsync(t => t.FeeWaived);

            var agentEarnings = await _context.Agents
                .Include(a => a.Commissions)
                .Select(a => new
                {
                    a.Id,
                    a.StoreName,
                    a.AgentName,
                    a.City,
                    TotalCommissions =
                        a.Commissions.Sum(c => c.Amount),
                    PaidCommissions =
                        a.Commissions
                            .Where(c => c.IsPaid)
                            .Sum(c => c.Amount),
                    UnpaidCommissions =
                        a.Commissions
                            .Where(c => !c.IsPaid)
                            .Sum(c => c.Amount),
                    CommissionCount = a.Commissions.Count
                })
                .ToListAsync();

            var feePolicy = await _context.FeePolicies
                .FirstOrDefaultAsync();

            ViewBag.TotalFees = totalFees;
            ViewBag.FreeTransactions = freeTransactions;
            ViewBag.AgentEarnings = agentEarnings;
            ViewBag.FeePolicy = feePolicy;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateGuestUser()
        {
            var guestId = Guid.NewGuid().ToString("N")
                .Substring(0, 8).ToUpper();
            var email = $"guest_{guestId}@cedarpay.guest";
            var password = $"Guest@{guestId}123";

            var guest = new User
            {
                UserName = email,
                Email = email,
                FirstName = "Guest",
                LastName = guestId,
                EmailConfirmed = true,
                IsActive = true,
                ProfileCompleted = true,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(
                guest, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(
                    guest, "user");

                var account = new Account
                {
                    SerialNumber = GenerateAdminSerial("ACC"),
                    UserId = guest.Id,
                    IsActive = true,
                    IsDefault = true,
                    CreatedAt = DateTime.Now
                };
                await _accountRepository.AddAsync(account);

                var wallet = new Wallet
                {
                    SerialNumber = GenerateAdminSerial("WAL"),
                    Balance = 1000m, 
                    IsActive = true,
                    IsDefault = true,
                    CreatedAt = DateTime.Now,
                    UserId = guest.Id,
                    AccountId = account.Id,
                    CurrencyId = 1 
                };
                await _walletRepository.AddAsync(wallet);

                TempData["Success"] =
                    $"Guest account created!\n" +
                    $"Email: {email}\n" +
                    $"Password: {password}\n" +
                    $"Balance: $1,000 USD (demo)";
            }
            else
            {
                TempData["Error"] =
                    "Failed to create guest: " +
                    string.Join(", ",
                        result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Users");
        }

        public async Task<IActionResult> Reports()
        {
            var totalFees = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && !t.FeeWaived)
                .SumAsync(t => (decimal?)t.FeeAmount) ?? 0;

            var totalCommissions = await _context.Commissions.SumAsync(c => (decimal?)c.Amount) ?? 0;

            var totalVolume = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            ViewBag.TotalFees = totalFees;
            ViewBag.TotalCommissions = totalCommissions;
            ViewBag.TotalVolume = totalVolume;
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalAgents = await _context.Agents.CountAsync(a => a.Status == AgentStatus.Approved);
            ViewBag.TotalTransactions = await _context.Transactions.CountAsync();

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportUsers(string role = "", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var users = await _userRepository.GetAllAsync();
            var userList = users.AsQueryable();

            if (!string.IsNullOrEmpty(role))
            {
                var userIdsInRole = new List<string>();
                foreach (var user in userList)
                {
                    var userRoles = await _userManager.GetRolesAsync(user);
                    if (userRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                        userIdsInRole.Add(user.Id);
                }
                userList = userList.Where(u => userIdsInRole.Contains(u.Id));
            }

            if (fromDate.HasValue)
                userList = userList.Where(u => u.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                userList = userList.Where(u => u.CreatedAt <= toDate.Value);

            var data = userList.ToList();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Users");

            ws.Cell(1, 1).Value = "Created At";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "Phone";
            ws.Cell(1, 5).Value = "Nationality";
            ws.Cell(1, 6).Value = "Account Type";
            ws.Cell(1, 7).Value = "KYC Verified";
            ws.Cell(1, 8).Value = "Status";
            ws.Cell(1, 9).Value = "Roles";

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894");
            headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            int row = 2;
            foreach (var u in data)
            {
                var userRoles = await _userManager.GetRolesAsync(u);
                ws.Cell(row, 1).Value = u.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 2).Value = $"{u.FirstName} {u.LastName}";
                ws.Cell(row, 3).Value = u.Email ?? "";
                ws.Cell(row, 4).Value = u.PhoneNumber ?? "";
                ws.Cell(row, 5).Value = u.Nationality ?? "";
                ws.Cell(row, 6).Value = u.AccountType ?? "Individual";
                ws.Cell(row, 7).Value = u.IsVerified ? "Yes" : "No";
                ws.Cell(row, 8).Value = u.IsActive ? "Active" : "Disabled";
                ws.Cell(row, 9).Value = string.Join(", ", userRoles);
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var filename = $"Users_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactions(DateTime? fromDate = null, DateTime? toDate = null, string status = "")
        {
            var transactions = _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .AsQueryable();

            if (fromDate.HasValue)
                transactions = transactions.Where(t => t.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                transactions = transactions.Where(t => t.CreatedAt <= toDate.Value);
            if (!string.IsNullOrEmpty(status))
                transactions = transactions.Where(t => t.Status.ToString() == status);

            var data = await transactions.OrderByDescending(t => t.CreatedAt).ToListAsync();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Transactions");

            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Serial Number";
            ws.Cell(1, 3).Value = "Sender";
            ws.Cell(1, 4).Value = "Receiver";
            ws.Cell(1, 5).Value = "Amount";
            ws.Cell(1, 6).Value = "Currency";
            ws.Cell(1, 7).Value = "Fee";
            ws.Cell(1, 8).Value = "Fee Waived";
            ws.Cell(1, 9).Value = "Status";
            ws.Cell(1, 10).Value = "Type";

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894");
            headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            int row = 2;
            foreach (var t in data)
            {
                ws.Cell(row, 1).Value = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 2).Value = t.SerialNumber ?? "";
                ws.Cell(row, 3).Value = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "";
                ws.Cell(row, 4).Value = t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : t.ReceiverName ?? "";
                ws.Cell(row, 5).Value = (double)t.Amount;
                ws.Cell(row, 6).Value = t.SenderCurrency?.Code ?? "";
                ws.Cell(row, 7).Value = (double)t.FeeAmount;
                ws.Cell(row, 8).Value = t.FeeWaived ? "Yes" : "No";
                ws.Cell(row, 9).Value = t.Status.ToString();
                ws.Cell(row, 10).Value = t.Type.ToString();
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var filename = $"Transactions_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCommissions(DateTime? fromDate = null, DateTime? toDate = null, bool? isPaid = null)
        {
            var commissions = _context.Commissions
                .Include(c => c.Agent)
                .Include(c => c.Transaction)
                .AsQueryable();

            if (fromDate.HasValue)
                commissions = commissions.Where(c => c.EarnedAt >= fromDate.Value);
            if (toDate.HasValue)
                commissions = commissions.Where(c => c.EarnedAt <= toDate.Value);
            if (isPaid.HasValue)
                commissions = commissions.Where(c => c.IsPaid == isPaid.Value);

            var data = await commissions.OrderByDescending(c => c.EarnedAt).ToListAsync();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.Worksheets.Add("Commissions");

            ws.Cell(1, 1).Value = "Earned Date";
            ws.Cell(1, 2).Value = "Agent Store";
            ws.Cell(1, 3).Value = "Agent Name";
            ws.Cell(1, 4).Value = "Transaction ID";
            ws.Cell(1, 5).Value = "Transaction Amount";
            ws.Cell(1, 6).Value = "Commission Rate (%)";
            ws.Cell(1, 7).Value = "Commission Amount";
            ws.Cell(1, 8).Value = "Status";
            ws.Cell(1, 9).Value = "Paid Date";

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#00b894");
            headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            int row = 2;
            foreach (var c in data)
            {
                ws.Cell(row, 1).Value = c.EarnedAt.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 2).Value = c.Agent?.StoreName ?? "";
                ws.Cell(row, 3).Value = c.Agent?.AgentName ?? "";
                ws.Cell(row, 4).Value = c.Transaction?.SerialNumber ?? "";
                ws.Cell(row, 5).Value = (double)(c.Transaction?.Amount ?? 0);
                ws.Cell(row, 6).Value = (double)(c.Percentage * 100);
                ws.Cell(row, 7).Value = (double)c.Amount;
                ws.Cell(row, 8).Value = c.IsPaid ? "Paid" : "Pending";
                ws.Cell(row, 9).Value = c.PaidAt?.ToString("yyyy-MM-dd") ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var filename = $"Commissions_Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
        }

        public async Task<IActionResult> ManageCommissions()
        {
            var agents = await _context.Agents
                .Where(a => a.Status == AgentStatus.Approved)
                .OrderBy(a => a.StoreName)
                .ToListAsync();

            var feePolicy = await _context.FeePolicies.FirstOrDefaultAsync();
            ViewBag.DefaultCommissionRate = feePolicy?.DefaultCommissionRate ?? 0.02m;

            return View(agents);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCommissionRate(int agentId, decimal commissionRate)
        {
            var agent = await _agentRepository.GetByIdAsync(agentId);
            if (agent == null)
            {
                return NotFound();
            }

            if (commissionRate < 0 || commissionRate > 1)
            {
                TempData["Error"] = "Commission rate must be between 0% and 100%.";
                return RedirectToAction("ManageCommissions");
            }

            agent.CommissionRate = commissionRate;
            await _agentRepository.UpdateAsync(agent);

            TempData["Success"] = $"Commission rate for {agent.StoreName} updated to {(commissionRate * 100).ToString("F1")}%";
            return RedirectToAction("ManageCommissions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDefaultCommissionRate(decimal defaultRate)
        {
            if (defaultRate < 0 || defaultRate > 1)
            {
                TempData["Error"] = "Default commission rate must be between 0% and 100%.";
                return RedirectToAction("ManageCommissions");
            }

            var feePolicy = await _context.FeePolicies.FirstOrDefaultAsync();
            if (feePolicy == null)
            {
                feePolicy = new FeePolicy();
                await _context.FeePolicies.AddAsync(feePolicy);
            }

            feePolicy.DefaultCommissionRate = defaultRate;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Default commission rate updated to {(defaultRate * 100).ToString("F1")}%";
            return RedirectToAction("ManageCommissions");
        }
    }
}