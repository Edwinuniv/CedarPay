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
        private readonly IUserRepository _userRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public TransactionController(
            IUserRepository userRepository,
            UserManager<User> userManager,
            ApplicationDbContext context)
            : base(userManager, userRepository)
        {
            _userRepository = userRepository;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

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

            var vm = new List<TransactionViewModel>();

            foreach (var t in sent)
            {
                string other;
                string? pic = null;

                if (t.Type == TransactionType.MobileTransfer)
                {
                    other = !string.IsNullOrEmpty(t.ReceiverName)
                        ? t.ReceiverName
                        : t.ReceiverPhoneNumber ?? "Mobile";
                }
                else
                {
                    other = t.ReceiverWallet?.User != null
                        ? $"{t.ReceiverWallet.User.FirstName} " +
                          $"{t.ReceiverWallet.User.LastName}"
                        : "Unknown";
                    pic = t.ReceiverWallet?.User?
                        .ProfilePictureUrl;
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
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt,
                    IsSent = true,
                    OtherPartyName = other,
                    OtherPartyPictureUrl = pic,
                    OtherPartyInitials = GetInitials(other),
                    ReceiverName = other,
                    SenderName = "",
                    ReceiverPictureUrl = pic,
                    CurrencySymbol =
                        t.SenderCurrency?.Symbol ?? "$",
                    SenderCurrency =
                        t.SenderCurrency?.Code ?? "",
                    ReceiverCurrency =
                        t.ReceiverCurrency?.Code ?? "",
                    SenderWalletSerial =
                        t.SenderWallet?.SerialNumber ?? "",
                    ReceiverWalletSerial =
                        t.ReceiverWallet?.SerialNumber
                        ?? t.ReceiverPhoneNumber ?? ""
                });
            }

            foreach (var t in received)
            {
                string other = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "Unknown Sender";
                string? pic =
                    t.SenderWallet?.User?.ProfilePictureUrl;

                vm.Add(new TransactionViewModel
                {
                    Id = t.Id,
                    SerialNumber = t.SerialNumber ?? "",
                    Amount = t.ConvertedAmount > 0
                        ? t.ConvertedAmount : t.Amount,
                    ConvertedAmount = t.ConvertedAmount,
                    FeeAmount = t.FeeAmount,
                    FeeWaived = t.FeeWaived,
                    ExchangeRateUsed = t.ExchangeRateUsed,
                    Description = t.Description,
                    Status = t.Status.ToString(),
                    Type = FormatType(t.Type),
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt,
                    IsSent = false,
                    OtherPartyName = other,
                    OtherPartyPictureUrl = pic,
                    OtherPartyInitials = GetInitials(other),
                    ReceiverName = other,
                    SenderName = other,
                    ReceiverPictureUrl = pic,
                    CurrencySymbol =
                        t.ReceiverCurrency?.Symbol ?? "$",
                    SenderCurrency =
                        t.SenderCurrency?.Code ?? "",
                    ReceiverCurrency =
                        t.ReceiverCurrency?.Code ?? "",
                    SenderWalletSerial =
                        t.SenderWallet?.SerialNumber ?? "",
                    ReceiverWalletSerial =
                        t.ReceiverWallet?.SerialNumber ?? ""
                });
            }

            return View(vm
                .OrderByDescending(t => t.CreatedAt)
                .ToList());
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var t = await _context.Transactions
                .Include(tx => tx.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(tx => tx.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(tx => tx.SenderCurrency)
                .Include(tx => tx.ReceiverCurrency)
                .FirstOrDefaultAsync(tx => tx.Id == id);

            if (t == null) return NotFound();

            bool isSent = t.SenderWallet?.UserId == userId;

            string other;
            string? pic = null;

            if (isSent)
            {
                if (t.Type == TransactionType.MobileTransfer)
                {
                    other = !string.IsNullOrEmpty(t.ReceiverName)
                        ? t.ReceiverName
                        : t.ReceiverPhoneNumber ?? "Mobile";
                }
                else
                {
                    other = t.ReceiverWallet?.User != null
                        ? $"{t.ReceiverWallet.User.FirstName} " +
                          $"{t.ReceiverWallet.User.LastName}"
                        : "Unknown";
                    pic = t.ReceiverWallet?.User?
                        .ProfilePictureUrl;
                }
            }
            else
            {
                other = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "Unknown Sender";
                pic = t.SenderWallet?.User?.ProfilePictureUrl;
            }

            var vm = new TransactionViewModel
            {
                Id = t.Id,
                SerialNumber = t.SerialNumber ?? "",
                Amount = isSent ? t.Amount
                    : (t.ConvertedAmount > 0
                        ? t.ConvertedAmount : t.Amount),
                ConvertedAmount = t.ConvertedAmount,
                FeeAmount = t.FeeAmount,
                FeeWaived = t.FeeWaived,
                ExchangeRateUsed = t.ExchangeRateUsed,
                Description = t.Description,
                Status = t.Status.ToString(),
                Type = FormatType(t.Type),
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                IsSent = isSent,
                OtherPartyName = other,
                OtherPartyPictureUrl = pic,
                OtherPartyInitials = GetInitials(other),
                ReceiverName = other,
                SenderName = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "",
                ReceiverPictureUrl = pic,
                CurrencySymbol = isSent
                    ? t.SenderCurrency?.Symbol ?? "$"
                    : t.ReceiverCurrency?.Symbol ?? "$",
                SenderCurrency =
                    t.SenderCurrency?.Code ?? "",
                ReceiverCurrency =
                    t.ReceiverCurrency?.Code ?? "",
                SenderWalletSerial =
                    t.SenderWallet?.SerialNumber ?? "",
                ReceiverWalletSerial =
                    t.ReceiverWallet?.SerialNumber
                    ?? t.ReceiverPhoneNumber ?? ""
            };

            return View(vm);
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "??";
            var parts = name.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}"
                    .ToUpper();
            return name.Length >= 2
                ? name.Substring(0, 2).ToUpper()
                : name.ToUpper();
        }

        private static string FormatType(TransactionType t)
        => t switch
        {
            TransactionType.WalletToWallet
                => "Wallet to Wallet",
            TransactionType.MobileTransfer
                => "Mobile Transfer",
            TransactionType.QRPayment => "QR Payment",
            TransactionType.PaymentLink => "Payment Link",
            _ => t.ToString()
        };

        public async Task<IActionResult> Summary(int id)
        {
            var userId = _userManager.GetUserId(User);
            var t = await _context.Transactions
                .Include(tx => tx.SenderWallet)
                    .ThenInclude(w => w.User)
                .Include(tx => tx.ReceiverWallet)
                    .ThenInclude(w => w.User)
                .Include(tx => tx.SenderCurrency)
                .Include(tx => tx.ReceiverCurrency)
                .FirstOrDefaultAsync(tx => tx.Id == id);

            if (t == null) return NotFound();

            bool isSent = t.SenderWallet?.UserId == userId;
            string other;

            if (isSent)
            {
                other = t.ReceiverWallet?.User != null
                    ? $"{t.ReceiverWallet.User.FirstName} " +
                      $"{t.ReceiverWallet.User.LastName}"
                    : t.ReceiverName ?? "Mobile Transfer";
            }
            else
            {
                other = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "Unknown";
            }

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
                CreatedAt = t.CreatedAt,
                IsSent = isSent,
                OtherPartyName = other,
                OtherPartyInitials = GetInitials(other),
                SenderName = t.SenderWallet?.User != null
                    ? $"{t.SenderWallet.User.FirstName} " +
                      $"{t.SenderWallet.User.LastName}"
                    : "",
                ReceiverName = other,
                CurrencySymbol = isSent
                    ? t.SenderCurrency?.Symbol ?? "$"
                    : t.ReceiverCurrency?.Symbol ?? "$",
                SenderCurrency = t.SenderCurrency?.Code ?? "",
                ReceiverCurrency = t.ReceiverCurrency?.Code ?? "",
                SenderWalletSerial = t.SenderWallet?.SerialNumber ?? "",
                ReceiverWalletSerial =
                    t.ReceiverWallet?.SerialNumber
                    ?? t.ReceiverPhoneNumber ?? ""
            };

            return View(vm);
        }
    }
}