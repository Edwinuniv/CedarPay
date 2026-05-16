using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class TransactionController : BaseController
    {
        private readonly IWalletRepository _walletRepository;
        private readonly ApplicationDbContext _context;

        public TransactionController(IUserRepository userRepository, UserManager<User> userManager, IWalletRepository walletRepository, ApplicationDbContext context): base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, decimal? minAmount, decimal? maxAmount, string? type, string? status)
        {
            var userId = _userManager.GetUserId(User);

            var sent = await _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.SenderWallet != null && t.SenderWallet.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var received = await _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w.User)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w.User)
                .Include(t => t.SenderCurrency)
                .Include(t => t.ReceiverCurrency)
                .Where(t => t.ReceiverWallet != null && t.ReceiverWallet.UserId == userId
                         && (t.SenderWallet == null || t.SenderWallet.UserId != userId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var userWallets = await _walletRepository.GetByUserIdAsync(userId);
            var walletIds = userWallets.Select(w => w.Id).ToList();

            var cashIns = await _context.TopUps
                .Include(t => t.Wallet).ThenInclude(w => w.Currency)
                .Include(t => t.Wallet).ThenInclude(w => w.User)
                .Where(t => walletIds.Contains(t.WalletId) && t.Method == TopUpMethod.Cash && t.Status == TopUpStatus.Completed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var vm = new List<TransactionViewModel>();

            foreach (var t in sent)
            {
                string other;
                string? pic = null;

                if (t.Type == TransactionType.MobileTransfer)
                {
                    other = !string.IsNullOrEmpty(t.ReceiverName) ? t.ReceiverName : t.ReceiverPhoneNumber ?? "Mobile";
                }
                else
                {
                    other = t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : "Unknown";
                    pic = t.ReceiverWallet?.User?.ProfilePictureUrl;
                }

                vm.Add(new TransactionViewModel
                {
                    Id = t.Id,
                    SerialNumber = t.SerialNumber ?? "",
                    Amount = t.Amount,
                    ConvertedAmount = t.ConvertedAmount,
                    FeeAmount = t.FeeAmount,
                    FeeWaived = t.FeeWaived,
                    ExchangeRateUsed = t.ExchangeRateUsed,
                    Description = t.Description,
                    Status = t.Status.ToString(),
                    Type = FormatType(t.Type),
                    TransactionType = "Transfer",
                    Category = t.Category.ToString(),
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt,
                    IsSent = true,
                    OtherPartyName = other,
                    OtherPartyPictureUrl = pic,
                    OtherPartyInitials = GetInitials(other),
                    ReceiverName = other,
                    SenderName = "",
                    ReceiverPictureUrl = pic,
                    CurrencySymbol = t.SenderCurrency?.Symbol ?? "$",
                    SenderCurrency = t.SenderCurrency?.Code ?? "",
                    ReceiverCurrency = t.ReceiverCurrency?.Code ?? "",
                    SenderWalletSerial = t.SenderWallet?.SerialNumber ?? "",
                    ReceiverWalletSerial = t.ReceiverWallet?.SerialNumber ?? t.ReceiverPhoneNumber ?? ""
                });
            }

            foreach (var t in received)
            {
                string other = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "Unknown Sender";
                string? pic = t.SenderWallet?.User?.ProfilePictureUrl;

                vm.Add(new TransactionViewModel
                {
                    Id = t.Id,
                    SerialNumber = t.SerialNumber ?? "",
                    Amount = t.ConvertedAmount > 0 ? t.ConvertedAmount : t.Amount,
                    ConvertedAmount = t.ConvertedAmount,
                    FeeAmount = t.FeeAmount,
                    FeeWaived = t.FeeWaived,
                    ExchangeRateUsed = t.ExchangeRateUsed,
                    Description = t.Description,
                    Status = t.Status.ToString(),
                    Type = FormatType(t.Type),
                    TransactionType = "Transfer",
                    Category = t.Category.ToString(),
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt,
                    IsSent = false,
                    OtherPartyName = other,
                    OtherPartyPictureUrl = pic,
                    OtherPartyInitials = GetInitials(other),
                    ReceiverName = other,
                    SenderName = other,
                    ReceiverPictureUrl = pic,
                    CurrencySymbol = t.ReceiverCurrency?.Symbol ?? "$",
                    SenderCurrency = t.SenderCurrency?.Code ?? "",
                    ReceiverCurrency = t.ReceiverCurrency?.Code ?? "",
                    SenderWalletSerial = t.SenderWallet?.SerialNumber ?? "",
                    ReceiverWalletSerial = t.ReceiverWallet?.SerialNumber ?? ""
                });
            }

            foreach (var cashIn in cashIns)
            {
                vm.Add(new TransactionViewModel
                {
                    Id = cashIn.Id,
                    SerialNumber = cashIn.PaymentReference ?? $"CASH-{cashIn.Id}",
                    Amount = cashIn.Amount,
                    FeeAmount = 0,
                    FeeWaived = true,
                    Description = cashIn.Description ?? "Cash deposit",
                    Status = cashIn.Status.ToString(),
                    Type = "Cash In",
                    TransactionType = "CashIn",
                    Category = "General",
                    CreatedAt = cashIn.CreatedAt,
                    IsSent = true,
                    OtherPartyName = "Cash Deposit",
                    OtherPartyInitials = "💵",
                    CurrencySymbol = cashIn.Wallet?.Currency?.Symbol ?? "$",
                    SenderCurrency = cashIn.Wallet?.Currency?.Code ?? "USD",
                    ReceiverCurrency = cashIn.Wallet?.Currency?.Code ?? "USD",
                    SenderWalletSerial = cashIn.Wallet?.SerialNumber ?? "",
                    ReceiverWalletSerial = cashIn.Wallet?.SerialNumber ?? ""
                });
            }

            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ViewBag.TotalCashInThisMonth = cashIns.Where(c => c.CreatedAt >= startOfMonth).Sum(c => c.Amount);
            ViewBag.TotalCashInAllTime = cashIns.Sum(c => c.Amount);

            ViewBag.Search = search;
            ViewBag.MinAmount = minAmount;
            ViewBag.MaxAmount = maxAmount;
            ViewBag.FilterType = type;
            ViewBag.FilterStatus = status;

            var result = vm.OrderByDescending(t => t.CreatedAt).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.ToLower();
                result = result.Where(t =>
                    t.SerialNumber.ToLower().Contains(q) ||
                    t.OtherPartyName.ToLower().Contains(q) ||
                    (t.Description ?? "").ToLower().Contains(q) ||
                    t.Category.ToLower().Contains(q)
                ).ToList();
            }

            if (minAmount.HasValue)
            {
                result = result.Where(t => t.Amount >= minAmount.Value).ToList();
            }
            if (maxAmount.HasValue)
            {
                result = result.Where(t => t.Amount <= maxAmount.Value).ToList();
            }
            if (!string.IsNullOrEmpty(type) && type != "All")
            {
                result = result.Where(t => t.TransactionType == type).ToList();
            }
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                result = result.Where(t => t.Status == status).ToList();
            }
            return View(result);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var topUp = await _context.TopUps
                .Include(t => t.Wallet).ThenInclude(w => w.Currency)
                .Include(t => t.Wallet).ThenInclude(w => w.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topUp != null && topUp.Wallet.UserId == userId)
            {
                var vm = new TransactionViewModel
                {
                    Id = topUp.Id,
                    SerialNumber = topUp.PaymentReference ?? $"CASH-{topUp.Id}",
                    Amount = topUp.Amount,
                    FeeAmount = 0,
                    FeeWaived = true,
                    Description = topUp.Description ?? "Cash deposit",
                    Status = topUp.Status.ToString(),
                    Type = "Cash In",
                    TransactionType = "CashIn",
                    Category = "General",
                    CreatedAt = topUp.CreatedAt,
                    IsSent = true,
                    OtherPartyName = "Cash Deposit",
                    OtherPartyInitials = "💵",
                    CurrencySymbol = topUp.Wallet?.Currency?.Symbol ?? "$",
                    SenderCurrency = topUp.Wallet?.Currency?.Code ?? "USD",
                    ReceiverCurrency = topUp.Wallet?.Currency?.Code ?? "USD",
                    SenderWalletSerial = topUp.Wallet?.SerialNumber ?? "",
                    ReceiverWalletSerial = topUp.Wallet?.SerialNumber ?? "",
                    ReceiverName = "My Wallet",
                    SenderName = "Cash Deposit"
                };
                return View(vm);
            }

            var t = await _context.Transactions
                .Include(tx => tx.SenderWallet).ThenInclude(w => w.User)
                .Include(tx => tx.ReceiverWallet).ThenInclude(w => w.User)
                .Include(tx => tx.SenderCurrency)
                .Include(tx => tx.ReceiverCurrency)
                .FirstOrDefaultAsync(tx => tx.Id == id);

            if (t == null)
            {
                return NotFound();
            }
            bool isSent = t.SenderWallet?.UserId == userId;
            string other;
            string? pic = null;

            if (isSent)
            {
                if (t.Type == TransactionType.MobileTransfer)
                {
                    other = !string.IsNullOrEmpty(t.ReceiverName) ? t.ReceiverName : t.ReceiverPhoneNumber ?? "Mobile";
                }
                else
                {
                    other = t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : "Unknown";
                    pic = t.ReceiverWallet?.User?.ProfilePictureUrl;
                }
            }
            else
            {
                other = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "Unknown Sender";
                pic = t.SenderWallet?.User?.ProfilePictureUrl;
            }

            var transactionVm = new TransactionViewModel
            {
                Id = t.Id,
                SerialNumber = t.SerialNumber ?? "",
                Amount = isSent ? t.Amount : (t.ConvertedAmount > 0 ? t.ConvertedAmount : t.Amount),
                ConvertedAmount = t.ConvertedAmount,
                FeeAmount = t.FeeAmount,
                FeeWaived = t.FeeWaived,
                ExchangeRateUsed = t.ExchangeRateUsed,
                Description = t.Description,
                Status = t.Status.ToString(),
                Type = FormatType(t.Type),
                TransactionType = "Transfer",
                Category = t.Category.ToString(),
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                IsSent = isSent,
                OtherPartyName = other,
                OtherPartyPictureUrl = pic,
                OtherPartyInitials = GetInitials(other),
                ReceiverName = other,
                SenderName = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "",
                ReceiverPictureUrl = pic,
                CurrencySymbol = isSent ? t.SenderCurrency?.Symbol ?? "$" : t.ReceiverCurrency?.Symbol ?? "$",
                SenderCurrency = t.SenderCurrency?.Code ?? "",
                ReceiverCurrency = t.ReceiverCurrency?.Code ?? "",
                SenderWalletSerial = t.SenderWallet?.SerialNumber ?? "",
                ReceiverWalletSerial = t.ReceiverWallet?.SerialNumber ?? t.ReceiverPhoneNumber ?? ""
            };

            return View(transactionVm);
        }

        public async Task<IActionResult> Summary(int id)
        {
            var userId = _userManager.GetUserId(User);
            var t = await _context.Transactions
                .Include(tx => tx.SenderWallet).ThenInclude(w => w.User)
                .Include(tx => tx.ReceiverWallet).ThenInclude(w => w.User)
                .Include(tx => tx.SenderCurrency)
                .Include(tx => tx.ReceiverCurrency)
                .FirstOrDefaultAsync(tx => tx.Id == id);

            if (t == null)
            {
                return NotFound();
            }
            bool isSent = t.SenderWallet?.UserId == userId;
            string other = isSent ? (t.ReceiverWallet?.User != null ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}" : t.ReceiverName ?? "Mobile Transfer") : (t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "Unknown");

            var vm = new TransactionViewModel
            {
                Id = t.Id,
                SerialNumber = t.SerialNumber ?? "",
                Amount = t.Amount,
                ConvertedAmount = t.ConvertedAmount,
                FeeAmount = t.FeeAmount,
                FeeWaived = t.FeeWaived,
                ExchangeRateUsed = t.ExchangeRateUsed,
                Description = t.Description,
                Status = t.Status.ToString(),
                Type = FormatType(t.Type),
                TransactionType = "Transfer",
                Category = t.Category.ToString(),
                CreatedAt = t.CreatedAt,
                IsSent = isSent,
                OtherPartyName = other,
                OtherPartyInitials = GetInitials(other),
                SenderName = t.SenderWallet?.User != null ? $"{t.SenderWallet.User.FirstName} {t.SenderWallet.User.LastName}" : "",
                ReceiverName = other,
                CurrencySymbol = isSent ? t.SenderCurrency?.Symbol ?? "$" : t.ReceiverCurrency?.Symbol ?? "$",
                SenderCurrency = t.SenderCurrency?.Code ?? "",
                ReceiverCurrency = t.ReceiverCurrency?.Code ?? "",
                SenderWalletSerial = t.SenderWallet?.SerialNumber ?? "",
                ReceiverWalletSerial = t.ReceiverWallet?.SerialNumber ?? t.ReceiverPhoneNumber ?? ""
            };

            return View(vm);
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private static string FormatType(TransactionType t) => t switch
        {
            TransactionType.WalletToWallet => "Wallet to Wallet",
            TransactionType.MobileTransfer => "Mobile Transfer",
            TransactionType.QRPayment => "QR Payment",
            TransactionType.PaymentLink => "Payment Link",
            _ => t.ToString()
        };
    }
}