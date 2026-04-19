using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class TopUpController : BaseController
    {
        private readonly ITopUpRepository _topUpRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly UserManager<User> _userManager;

        public TopUpController(ITopUpRepository topUpRepository, IWalletRepository walletRepository,
            INotificationRepository notificationRepository, UserManager<User> userManager,
            IUserRepository userRepository): base(userManager, userRepository)
        {
            _topUpRepository = topUpRepository;
            _walletRepository = walletRepository;
            _notificationRepository = notificationRepository;
            _userManager = userManager;
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            var vm = new TopUpViewModel
            {
                UserWallets = wallets.Select(w => new WalletSummaryViewModel
                {
                    Id = w.Id,
                    SerialNumber = w.SerialNumber,
                    Balance = w.Balance,
                    CurrencyCode = w.Currency?.Code ?? "",
                    CurrencySymbol = w.Currency?.Symbol ?? "",
                    CurrencyName = w.Currency?.Name ?? "",
                    IsDefault = w.IsDefault
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Store(TopUpViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("Create", vm);

            var userId = _userManager.GetUserId(User);
            var wallet = await _walletRepository.GetByIdAsync(vm.WalletId);

            if (wallet == null || wallet.UserId != userId)
            {
                ModelState.AddModelError("", "Invalid wallet.");
                return View("Create", vm);
            }

            var topUp = new TopUp
            {
                Amount = vm.Amount,
                Status = TopUpStatus.Completed,
                Method = vm.Method == "CreditCard"
                    ? TopUpMethod.CreditCard
                    : TopUpMethod.DebitCard,
                Description = vm.Description,
                WalletId = vm.WalletId,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference = $"REF-{DateTime.Now.Ticks}"
            };

            await _topUpRepository.AddAsync(topUp);

            wallet.Balance += vm.Amount;
            await _walletRepository.UpdateAsync(wallet);

            await _notificationRepository.AddAsync(new Notification
            {
                Title = "Top Up Successful!",
                Message = $"Your wallet has been topped up with " +
                          $"{vm.Amount:F2} {wallet.Currency?.Symbol}",
                Type = NotificationType.TopUpCompleted,
                UserId = userId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            TempData["Success"] = $"Wallet topped up successfully with " +
                                  $"{vm.Amount:F2}!";
            return RedirectToAction("Index", "Wallet");
        }
    }
}