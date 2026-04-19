using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Implementations;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : BaseController
    {
        private readonly IUserRepository _userRepository;
        private readonly IAgentRepository _agentRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IReviewRepository _reviewRepository;
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly IWalletRepository _walletRepository;

        public AdminController(IUserRepository userRepository, IAgentRepository agentRepository,
            ITransactionRepository transactionRepository,IReviewRepository reviewRepository,
            IAgentApplicationRepository applicationRepository,
            UserManager<User> userManager, ApplicationDbContext context,
            IAccountRepository accountRepository, IWalletRepository walletRepository): base(userManager, userRepository)
        {
            _userRepository = userRepository;
            _agentRepository = agentRepository;
            _transactionRepository = transactionRepository;
            _reviewRepository = reviewRepository;
            _applicationRepository = applicationRepository;
            _userManager = userManager;
            _context = context;
            _accountRepository = accountRepository; 
            _walletRepository = walletRepository;
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

            await _context.SaveChangesAsync();
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

            TempData["Success"] = $"Wallet approved for {request.User?.FirstName}!";
            return RedirectToAction("WalletRequests");
        }

        [HttpPost]
        public async Task<IActionResult> RejectWallet(
            int id, string reason)
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
    }
}