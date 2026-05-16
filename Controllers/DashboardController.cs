using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;
using MoneyTransfer.ViewModels;
using System.Text;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class DashboardController : BaseController
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAgentRepository _agentRepository;
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly ApplicationDbContext _context;


        public DashboardController(IUserRepository userRepository, IWalletRepository walletRepository, IBeneficiaryRepository beneficiaryRepository, IAccountRepository accountRepository, IAgentRepository agentRepository, IAgentApplicationRepository applicationRepository, UserManager<User> userManager, ApplicationDbContext context): base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _accountRepository = accountRepository;
            _agentRepository = agentRepository;
            _applicationRepository = applicationRepository;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return NotFound();

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

            if (User.IsInRole(Roles.Agent) || User.IsInRole(Roles.Admin))
            {
                var agentList = (await _agentRepository.GetByUserIdAsync(userId)).ToList();

                Agent? selectedAgent = null;
                if (Request.Cookies.TryGetValue("SelectedAgentId", out var cookieId) &&
                    int.TryParse(cookieId, out var agentId))
                {
                    selectedAgent = agentList.FirstOrDefault(a => a.Id == agentId);
                }
                selectedAgent ??= agentList.FirstOrDefault();

                ViewBag.AgentStore = selectedAgent;
                ViewBag.AgentStores = agentList;
                ViewBag.AgentLocationSet = selectedAgent?.Latitude != 0;

                if (selectedAgent != null)
                {
                    ViewBag.AgentTotalCashIn = await _context.TopUps
                        .Where(t => t.Method == TopUpMethod.Cash)
                        .SumAsync(t => (decimal?)t.Amount) ?? 0;
                    ViewBag.AgentTotalCommissions = await _context.Commissions
                        .Where(c => c.AgentId == selectedAgent.Id)
                        .SumAsync(c => (decimal?)c.Amount) ?? 0;
                }
                else
                {
                    ViewBag.AgentTotalCashIn = 0;
                    ViewBag.AgentTotalCommissions = 0;
                }
            }

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

        public async Task<IActionResult> TestEmail()
        {
            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();

                await emailService.SendAsync(
                    "edwinmouawad82@gmail.com",
                    "Test User",
                    "Test Email from CedarPay",
                    "<h1>✅ Working!</h1><p>If you see this, email is configured correctly.</p>"
                );

                TempData["Success"] = "Test email sent successfully! Check your inbox.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Email failed: {ex.Message}";

                Console.WriteLine($"ERROR TYPE: {ex.GetType().Name}");
                Console.WriteLine($"ERROR MESSAGE: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"INNER ERROR: {ex.InnerException.Message}");
                    TempData["Error"] += $" | Inner: {ex.InnerException.Message}";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TestForgotPasswordEmail()
        {
            var results = new List<string>();

            try
            {
                var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();

                var user = await userManager.FindByEmailAsync("edwinmouawad82@gmail.com");

                if (user == null)
                {
                    results.Add("❌ User not found!");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"✅ User found: {user.Email}");

                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                results.Add($"✅ Reset link generated: {callbackUrl}");

                var html = $@"
            <h2 style='color:#00b894'>Reset Your Password</h2>
            <p>Click the button below to reset your password:</p>
            <a href='{callbackUrl}' style='background:#00b894;color:#fff;padding:10px 20px;text-decoration:none;border-radius:5px;'>Reset Password</a>";

                await emailService.SendAsync(
                    user.Email,
                    $"{user.FirstName} {user.LastName}",
                    "CedarPay — Reset Your Password",
                    html);

                results.Add("✅ Email sent successfully!");
                TempData["Success"] = string.Join(" | ", results);
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                results.Add($"Stack: {ex.StackTrace}");
                TempData["Error"] = string.Join(" | ", results);

                Console.WriteLine($"ERROR: {ex.ToString()}");
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TestNewEmailAccount()
        {
            var results = new List<string>();

            try
            {
                var emailService = HttpContext.RequestServices.GetRequiredService<IEmailService>();
                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();

                var username = config["Email:Username"];
                var fromEmail = config["Email:FromEmail"];

                results.Add($"Sending from: {fromEmail}");
                results.Add($"Using username: {username}");

                await emailService.SendAsync(
                    "edwinmouawad82@gmail.com", 
                    "Edwin",
                    "Test from New CedarPay Email",
                    "<h1>✅ Success!</h1><p>This email was sent from the new CedarPay notifications account!</p>" +
                    $"<p>From: {fromEmail}</p>" +
                    $"<p>Time: {DateTime.Now}</p>"
                );

                results.Add("✅ Email sent successfully from new account!");
                TempData["Success"] = string.Join(" | ", results);
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> DebugUserLogin(string email)
        {
            var results = new List<string>();

            if (string.IsNullOrEmpty(email))
            {
                results.Add("❌ No email provided. Please add ?email=user@example.com to the URL");
                TempData["Error"] = string.Join(" | ", results);
                return RedirectToAction(nameof(Index));
            }

            try
            {
                email = System.Web.HttpUtility.UrlDecode(email);

                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found with email: {email}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"✅ User found: {user.Email}");
                results.Add($"User ID: {user.Id}");
                results.Add($"UserName: {user.UserName}");
                results.Add($"Lockout Enabled: {user.LockoutEnabled}");
                results.Add($"Lockout End: {user.LockoutEnd}");
                results.Add($"Email Confirmed: {user.EmailConfirmed}");
                results.Add($"Account Active: {user.IsActive}");
                results.Add($"Access Failed Count: {user.AccessFailedCount}");
                results.Add($"Password Hash: {(string.IsNullOrEmpty(user.PasswordHash) ? "NO PASSWORD" : "Has Password")}");

                if (await _userManager.IsLockedOutAsync(user))
                {
                    results.Add($"⚠️ Account IS LOCKED OUT!");
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    results.Add($"Lockout ends: {lockoutEnd}");
                }

                TempData["Success"] = string.Join(" | ", results);
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ConfirmUserEmail(string email)
        {
            var results = new List<string>();

            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found: {email}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"✅ User found: {user.Email}");
                results.Add($"Current Email Confirmed: {user.EmailConfirmed}");

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var result = await _userManager.ConfirmEmailAsync(user, token);

                if (result.Succeeded)
                {
                    results.Add($"✅ Email confirmed successfully!");

                    await _userManager.SetLockoutEndDateAsync(user, null);
                    results.Add($"✅ Lockout removed");

                    TempData["Success"] = string.Join(" | ", results);
                }
                else
                {
                    results.Add($"❌ Failed to confirm: {string.Join(", ", result.Errors)}");
                    TempData["Error"] = string.Join(" | ", results);
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> ResetSpecificUserPassword(string email, string newPassword = "Test123!")
        {
            var results = new List<string>();

            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found: {email}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                var removeResult = await _userManager.RemovePasswordAsync(user);
                if (removeResult.Succeeded)
                {
                    results.Add("✅ Old password removed");
                }

                var addResult = await _userManager.AddPasswordAsync(user, newPassword);
                if (addResult.Succeeded)
                {
                    results.Add($"✅ New password set: {newPassword}");
                    TempData["Success"] = string.Join(" | ", results);
                }
                else
                {
                    results.Add($"❌ Failed: {string.Join(", ", addResult.Errors)}");
                    TempData["Error"] = string.Join(" | ", results);
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> FullDiagnostic(string email)
        {
            var results = new List<string>();

            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found: {email}");
                    TempData["Error"] = string.Join("<br/>", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"USER ACCOUNT STATUS");
                results.Add($"Email: {user.Email}");
                results.Add($"User ID: {user.Id}");
                results.Add($"UserName: {user.UserName}");
                results.Add($"Email Confirmed: {user.EmailConfirmed}");
                results.Add($"Lockout Enabled: {user.LockoutEnabled}");
                results.Add($"Lockout End: {user.LockoutEnd}");
                results.Add($"Access Failed Count: {user.AccessFailedCount}");
                results.Add($"Account Active: {user.IsActive}");
                results.Add($"Two Factor Enabled: {user.TwoFactorEnabled}");
                results.Add($"Has Password Hash: {(string.IsNullOrEmpty(user.PasswordHash) ? "NO" : "YES")}");

                var testPassword = "Test123!";
                var isValid = await _userManager.CheckPasswordAsync(user, testPassword);
                results.Add($"Password '{testPassword}' valid: {isValid}");

                var roles = await _userManager.GetRolesAsync(user);
                results.Add($"Roles: {(roles.Any() ? string.Join(", ", roles) : "None")}");

                var canSignIn = await _userManager.IsEmailConfirmedAsync(user) &&
                                !await _userManager.IsLockedOutAsync(user);
                results.Add($"Can Sign In (by rules): {canSignIn}");

                TempData["Success"] = string.Join("<br/>", results);
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join("<br/>", results);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ForceResetPassword(string email, string newPassword = "Test123!")
        {
            var results = new List<string>();

            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found: {email}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"RESETTING PASSWORD FOR {user.Email}");

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                results.Add($"✅ Token generated");

                var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (resetResult.Succeeded)
                {
                    results.Add($"✅ Password successfully reset to: {newPassword}");

                    var verifyResult = await _userManager.CheckPasswordAsync(user, newPassword);
                    results.Add($"Password verification: {(verifyResult ? "✅ SUCCESS" : "❌ FAILED")}");

                    user.LockoutEnabled = false;
                    user.AccessFailedCount = 0;
                    await _userManager.UpdateAsync(user);
                    results.Add($"✅ Lockout disabled");

                    TempData["Success"] = string.Join(" | ", results);
                }
                else
                {
                    results.Add($"❌ Reset failed: {string.Join(", ", resetResult.Errors)}");
                    TempData["Error"] = string.Join(" | ", results);
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> RecreateUserAccount(string email, string newPassword = "Test123!")
        {
            var results = new List<string>();

            try
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    results.Add($"❌ User not found: {email}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"RECREATING ACCOUNT FOR {email}");

                var userId = user.Id;
                var userName = user.UserName;
                var firstName = user.FirstName;
                var lastName = user.LastName;

                var deleteResult = await _userManager.DeleteAsync(user);

                if (!deleteResult.Succeeded)
                {
                    results.Add($"❌ Delete failed: {string.Join(", ", deleteResult.Errors)}");
                    TempData["Error"] = string.Join(" | ", results);
                    return RedirectToAction(nameof(Index));
                }

                results.Add($"✅ User deleted");

                var newUser = new User
                {
                    UserName = userName,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var createResult = await _userManager.CreateAsync(newUser, newPassword);

                if (createResult.Succeeded)
                {
                    results.Add($"✅ New user created");
                    results.Add($"✅ Email: {email}");
                    results.Add($"✅ Password: {newPassword}");
                    results.Add($"✅ Email confirmed");
                    results.Add($"✅ Lockout disabled");

                    await _userManager.AddToRoleAsync(newUser, "User");
                    results.Add($"✅ Role 'User' added");

                    TempData["Success"] = string.Join(" | ", results);
                }
                else
                {
                    results.Add($"❌ Create failed: {string.Join(", ", createResult.Errors)}");
                    TempData["Error"] = string.Join(" | ", results);
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join(" | ", results);
            }

            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> CreateFreshUser()
        {
            var results = new List<string>();

            try
            {
                var newEmail = $"testuser_{DateTime.Now.Ticks}@example.com";
                var password = "Test123!";

                var newUser = new User
                {
                    UserName = newEmail,
                    Email = newEmail,
                    FirstName = "Test",
                    LastName = "User",
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    EmailConfirmed = true,
                    LockoutEnabled = false,
                    NormalizedEmail = newEmail.ToUpper(),
                    NormalizedUserName = newEmail.ToUpper()
                };

                var result = await _userManager.CreateAsync(newUser, password);

                if (result.Succeeded)
                {
                    results.Add($"✅ NEW USER CREATED!");
                    results.Add($"Email: {newEmail}");
                    results.Add($"Password: {password}");
                    results.Add($"---");
                    results.Add($"PLEASE TRY LOGGING IN WITH THESE CREDENTIALS");

                    var verified = await _userManager.CheckPasswordAsync(newUser, password);
                    results.Add($"Password verification: {(verified ? "✅ PASSED" : "❌ FAILED")}");

                    TempData["Success"] = string.Join("<br/>", results);
                }
                else
                {
                    results.Add($"❌ Failed: {string.Join(", ", result.Errors)}");
                    TempData["Error"] = string.Join("<br/>", results);
                }
            }
            catch (Exception ex)
            {
                results.Add($"❌ Error: {ex.Message}");
                TempData["Error"] = string.Join("<br/>", results);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}