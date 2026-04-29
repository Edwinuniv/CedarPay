using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using MoneyTransfer.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class TopUpController : BaseController
    {
        private readonly ITopUpRepository _topUpRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public TopUpController(
            ITopUpRepository topUpRepository,
            IWalletRepository walletRepository,
            INotificationRepository notificationRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            IConfiguration configuration,
            IEmailService emailService) : base(userManager, userRepository)
        {
            _configuration = configuration;
            _topUpRepository = topUpRepository;
            _walletRepository = walletRepository;
            _notificationRepository = notificationRepository;
            _emailService = emailService;
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository
                .GetByUserIdAsync(userId);

            var vm = new TopUpViewModel
            {
                UserWallets = wallets.Select(w =>
                    new WalletSummaryViewModel
                    {
                        Id = w.Id,
                        CurrencyCode = w.Currency?.Code ?? "",
                        CurrencySymbol = w.Currency?.Symbol ?? "$",
                        Balance = w.Balance,
                        IsDefault = w.IsDefault,
                        SerialNumber = w.SerialNumber ?? ""
                    }).ToList()
            };

            ViewBag.StripePublishableKey =
                _configuration["Stripe:PublishableKey"] ?? "";

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(
            TopUpViewModel vm,
            string? stripeToken)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.StripePublishableKey =
                    _configuration["Stripe:PublishableKey"] ?? "";
                return View("Create", vm);
            }

            var userId = _userManager.GetUserId(User);
            var wallet = await _walletRepository
                .GetByIdAsync(vm.WalletId);
            if (wallet == null) return NotFound();

            if (!Enum.TryParse<TopUpMethod>(vm.Method, true, out var selectedMethod))
            {
                ModelState.AddModelError("", "Invalid payment method.");
                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"] ?? "";
                return View("Create", vm);
            }

            if (selectedMethod == TopUpMethod.CreditCard && !string.IsNullOrEmpty(stripeToken))
            {
                try
                {
                    var chargeOptions = new Stripe.ChargeCreateOptions
                    {
                        Amount = (long)(vm.Amount * 100),
                        Currency = "usd",
                        Source = stripeToken,
                        Description = $"CedarPay Top-Up — {wallet.Currency?.Code} Wallet"
                    };

                    var chargeService = new Stripe.ChargeService();
                    var charge = await chargeService.CreateAsync(chargeOptions);

                    if (charge.Status != "succeeded")
                    {
                        TempData["Error"] = "Payment failed. Please try again.";
                        return RedirectToAction("Create");
                    }

                    wallet.Balance += vm.Amount;
                    await _walletRepository.UpdateAsync(wallet);

                    await _topUpRepository.AddAsync(new TopUp
                    {
                        Amount = vm.Amount,
                        Status = TopUpStatus.Completed,
                        Method = selectedMethod,
                        Description = vm.Description ?? "Card payment via Stripe",
                        WalletId = vm.WalletId,
                        CurrencyId = wallet.CurrencyId,
                        CreatedAt = DateTime.Now,
                        PaymentReference = charge.Id
                    });

                    var user = await _userManager.FindByIdAsync(userId);
                    if (user?.Email != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            await _emailService.SendNotificationAsync(
                                user.Email,
                                $"{user.FirstName} {user.LastName}",
                                "Wallet Top-Up Successful! 🎉",
                                $"Your wallet ({wallet.SerialNumber}) has been successfully topped up with {wallet.Currency?.Symbol}{vm.Amount:N2}.\n\n" +
                                $"New balance: {wallet.Currency?.Symbol}{wallet.Balance:N2}\n" +
                                $"Transaction reference: {charge.Id}\n\n" +
                                $"Thank you for choosing CedarPay!");
                        });
                    }

                    TempData["Success"] = $"✅ {wallet.Currency?.Symbol}{vm.Amount:N2} added successfully!";
                    return RedirectToAction("Index", "Wallet");
                }
                catch (Stripe.StripeException ex)
                {
                    TempData["Error"] = $"Payment error: {ex.Message}";
                    return RedirectToAction("Create");
                }
            }

            wallet.Balance += vm.Amount;
            await _walletRepository.UpdateAsync(wallet);

            await _topUpRepository.AddAsync(new TopUp
            {
                Amount = vm.Amount,
                Status = TopUpStatus.Completed,
                Method = selectedMethod,
                Description = vm.Description ?? "Bank Transfer",
                WalletId = vm.WalletId,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference = $"BANK-{DateTime.Now.Ticks}"
            });

            var bankUser = await _userManager.FindByIdAsync(userId);
            if (bankUser?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        bankUser.Email,
                        $"{bankUser.FirstName} {bankUser.LastName}",
                        "Wallet Top-Up Successful!",
                        $"Your wallet ({wallet.SerialNumber}) has been successfully topped up with {wallet.Currency?.Symbol}{vm.Amount:N2}.\n\n" +
                        $"New balance: {wallet.Currency?.Symbol}{wallet.Balance:N2}\n" +
                        $"Payment method: Bank Transfer\n" +
                        $"Transaction reference: BANK-{DateTime.Now.Ticks}\n\n" +
                        $"Thank you for choosing CedarPay!");
                });
            }

            TempData["Success"] = $"{wallet.Currency?.Symbol}{vm.Amount:N2} added to wallet!";
            return RedirectToAction("Index", "Wallet");
        }

        private async Task<IActionResult> CreateStripeCheckout(
            TopUpViewModel vm,
            Wallet wallet,
            string userId)
        {
            var publishableKey = _configuration["Stripe:PublishableKey"];

            Enum.TryParse<TopUpMethod>(vm.Method, true, out var selectedMethod);

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(vm.Amount * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"CedarPay Wallet Top-Up — {wallet.Currency?.Code}",
                                Description = $"Adding {wallet.Currency?.Symbol}{vm.Amount:N2} to your {wallet.Currency?.Code} wallet"
                            }
                        }
                    }
                },
                Mode = "payment",
                SuccessUrl = Url.Action("StripeSuccess", "TopUp",
                    new
                    {
                        walletId = vm.WalletId,
                        amount = vm.Amount,
                        method = selectedMethod.ToString()
                    }, Request.Scheme),
                CancelUrl = Url.Action("Create", "TopUp",
                    null, Request.Scheme)
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return Redirect(session.Url);
        }

        // Stripe calls this after successful payment
        public async Task<IActionResult> StripeSuccess(
            int walletId, decimal amount, string method)
        {
            var userId = _userManager.GetUserId(User);

            var wallet = await _walletRepository.GetByIdAsync(walletId);
            if (wallet == null) return NotFound();

            wallet.Balance += amount;
            await _walletRepository.UpdateAsync(wallet);

            Enum.TryParse<TopUpMethod>(method, true, out var methodEnum);

            await _topUpRepository.AddAsync(new TopUp
            {
                Amount = amount,
                Status = TopUpStatus.Completed,
                Method = methodEnum,
                Description = "Card payment via Stripe",
                WalletId = walletId,
                CurrencyId = wallet.CurrencyId,
                CreatedAt = DateTime.Now,
                PaymentReference = $"STRIPE-{DateTime.Now.Ticks}"
            });

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Wallet Top-Up Successful! 🎉",
                        $"Your wallet ({wallet.SerialNumber}) has been successfully topped up with {wallet.Currency?.Symbol}{amount:N2}.\n\n" +
                        $"New balance: {wallet.Currency?.Symbol}{wallet.Balance:N2}\n" +
                        $"Payment method: {methodEnum} via Stripe\n" +
                        $"Transaction reference: STRIPE-{DateTime.Now.Ticks}\n\n" +
                        $"Thank you for choosing CedarPay!");
                });
            }

            TempData["Success"] = $"Payment successful! {wallet.Currency?.Symbol}{amount:N2} added to your wallet! 🎉";
            return RedirectToAction("Index", "Wallet");
        }
    }
}