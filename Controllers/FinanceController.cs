using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class FinanceController : BaseController
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITopUpRepository _topUpRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly UserManager<User> _userManager;

        public FinanceController(
            ITransactionRepository transactionRepository,
            ITopUpRepository topUpRepository,
            IWalletRepository walletRepository,
            UserManager<User> userManager,
            IUserRepository userRepository)
            : base(userManager, userRepository)
        {
            _transactionRepository = transactionRepository;
            _topUpRepository = topUpRepository;
            _walletRepository = walletRepository;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var sent = (await _transactionRepository
                .GetSentByUserAsync(userId)).ToList();

            var received = (await _transactionRepository
                .GetReceivedByUserAsync(userId)).ToList();

            var topUps = (await _topUpRepository
                .GetByUserIdAsync(userId)).ToList();

            var monthly = new List<MonthlyFinanceViewModel>();
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.Now.AddMonths(-i);
                var sentMonth = sent
                    .Where(t => t.CreatedAt.Month == month.Month
                             && t.CreatedAt.Year == month.Year)
                    .Sum(t => t.Amount);
                var receivedMonth = received
                    .Where(t => t.CreatedAt.Month == month.Month
                             && t.CreatedAt.Year == month.Year)
                    .Sum(t => t.ConvertedAmount);

                monthly.Add(new MonthlyFinanceViewModel
                {
                    Month = month.ToString("MMM yyyy"),
                    TotalSent = sentMonth,
                    TotalReceived = receivedMonth
                });
            }

            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            var totalSentCount = sent
                .Count(t => t.Status == TransactionStatus.Completed);

            var transactionsSinceLastFree = totalSentCount % 10;

            var nextFreeIn = transactionsSinceLastFree == 0 && totalSentCount > 0
                ? 0
                : 10 - transactionsSinceLastFree;

            var vm = new FinanceViewModel
            {
                TotalSent = sent
                    .Where(t => t.Status == TransactionStatus.Completed)
                    .Sum(t => t.Amount),
                TotalReceived = received
                    .Where(t => t.Status == TransactionStatus.Completed)
                    .Sum(t => t.ConvertedAmount),
                TotalTopUps = topUps
                    .Where(t => t.Status == TopUpStatus.Completed)
                    .Sum(t => t.Amount),
                TotalFeesPaid = sent
                    .Where(t => !t.FeeWaived)
                    .Sum(t => t.FeeAmount),
                TotalFeesSaved = sent
                    .Where(t => t.FeeWaived)
                    .Sum(t => t.FeeAmount),
                TransactionCount = totalSentCount,
                FreeTransactionsUsed = sent.Count(t => t.FeeWaived),
                NextFreeIn = nextFreeIn,
                MonthlyBreakdown = monthly,
                WalletBalances = wallets.Select(w =>
                    new WalletSummaryViewModel
                    {
                        Id = w.Id,
                        CurrencyCode = w.Currency?.Code ?? "",
                        CurrencySymbol = w.Currency?.Symbol ?? "",
                        CurrencyName = w.Currency?.Name ?? "",
                        Balance = w.Balance,
                        IsDefault = w.IsDefault,
                        SerialNumber = w.SerialNumber
                    }).ToList(),
                RecentTopUps = topUps
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .Select(t => new TopUpSummaryViewModel
                    {
                        Amount = t.Amount,
                        Method = t.Method.ToString(),
                        Status = t.Status.ToString(),
                        CreatedAt = t.CreatedAt,
                        CurrencySymbol = t.Currency?.Symbol ?? "$"
                    }).ToList()
            };

            return View(vm);
        }
    }
}