using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Constants;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class TransferController : BaseController
    {
        private readonly IWalletRepository _walletRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly UserManager<User> _userManager;
        private readonly ICurrencyExchangeService _exchangeService;

        public TransferController(IWalletRepository walletRepository, ITransactionRepository transactionRepository,
            IBeneficiaryRepository beneficiaryRepository, ICurrencyRepository currencyRepository,
            INotificationRepository notificationRepository, IAccountRepository accountRepository,
            UserManager<User> userManager, IUserRepository userRepository,
            ICurrencyExchangeService exchangeService) : base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _transactionRepository = transactionRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _currencyRepository = currencyRepository;
            _notificationRepository = notificationRepository;
            _accountRepository = accountRepository;
            _userManager = userManager;
            _exchangeService = exchangeService;
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);
            var beneficiaries = await _beneficiaryRepository.GetByUserIdAsync(userId);

            var vm = new TransferViewModel
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
                }).ToList(),

                Beneficiaries = beneficiaries.Select(b => new BeneficiarySelectViewModel
                {
                    Id = b.Id,
                    Nickname = b.Nickname,
                    ReceiverName = b.ReceiverName,
                    ReceiverWalletSerial = b.ReceiverWalletSerial,
                    ReceiverPhoneNumber = b.ReceiverPhoneNumber,
                    Type = b.Type.ToString()
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Store(TransferViewModel vm)
        {
            var userId = _userManager.GetUserId(User);

            var senderWallet = await _walletRepository.GetByIdAsync(vm.SenderWalletId);
            if (senderWallet == null || senderWallet.UserId != userId)
            {
                ModelState.AddModelError("", "Invalid sender wallet.");
                return View("Create", await RepopulateTransferVM(vm, userId));
            }

            if (vm.TransferType == "WalletToWallet" &&
                !string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                var receiverCheck = await _walletRepository
                    .GetBySerialNumberAsync(vm.ReceiverWalletSerial);

                if (receiverCheck != null && receiverCheck.UserId == userId)
                {
                    if (receiverCheck.CurrencyId == senderWallet.CurrencyId)
                    {
                        ModelState.AddModelError("",
                            "You cannot send money to a wallet " +
                            "of the same currency. " +
                            "Use a different currency wallet.");
                        return View("Create",
                            await RepopulateTransferVM(vm, userId));
                    }
                }
            }

            if (vm.TransferType == "MobileTransfer")
            {
                var currentUser = await _userManager.FindByIdAsync(userId);
                if (currentUser != null &&
                    !string.IsNullOrEmpty(vm.ReceiverPhoneNumber) &&
                    currentUser.PhoneNumber == vm.ReceiverPhoneNumber)
                {
                    ModelState.AddModelError("",
                        "You cannot send money to your own phone number.");
                    return View("Create",
                        await RepopulateTransferVM(vm, userId));
                }
            }

            var hasFunds = await _walletRepository
                .HasSufficientBalenceAsync(vm.SenderWalletId, vm.Amount);
            if (!hasFunds)
            {
                ModelState.AddModelError("", "Insufficient balance.");
                return View("Create", await RepopulateTransferVM(vm, userId));
            }

            var transactionCount = await _transactionRepository
                .GetUserTransactionCountAsync(userId);
            var feePercentage = 0.02m;
            var fixedFee = 0.50m;
            var threshold = 10;

            var isFreeTransaction = (transactionCount + 1) % threshold == 0;
            var feeAmount = isFreeTransaction ? 0 :
                (vm.Amount * feePercentage) + fixedFee;

            Wallet? receiverWallet = null;
            if (vm.TransferType == "WalletToWallet" &&
                !string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                receiverWallet = await _walletRepository
                    .GetBySerialNumberAsync(vm.ReceiverWalletSerial);

                if (receiverWallet == null)
                {
                    ModelState.AddModelError("",
                        "Receiver wallet not found.");
                    return View("Create", await RepopulateTransferVM(vm, userId));
                }
            }

            var exchangeRate = 1m;
            var convertedAmount = vm.Amount;

            if (receiverWallet != null &&
                senderWallet.CurrencyId != receiverWallet.CurrencyId)
            {
                var senderCurrency = senderWallet.Currency;
                var receiverCurrency = receiverWallet.Currency;

                if (senderCurrency != null && receiverCurrency != null)
                {
                    decimal rate;
                    if (senderCurrency.Code == receiverCurrency.Code)
                    {
                        rate = 1m;
                    }
                    else
                    {
                        rate = await _exchangeService.GetRateAsync(
                            senderCurrency.Code, receiverCurrency.Code);
                    }

                    exchangeRate = rate;
                    convertedAmount = vm.Amount * rate;
                }
            }

            var transaction = new Transaction
            {
                SerialNumber = GenerateSerial("TXN"),
                Amount = vm.Amount,
                ConvertedAmount = convertedAmount,
                FeeAmount = feeAmount,
                FeeWaived = isFreeTransaction,
                ExchangeRateUsed = exchangeRate,
                Description = vm.Description,
                Status = TransactionStatus.Completed,
                Type = vm.TransferType == "WalletToWallet"
                    ? TransactionType.WalletToWallet
                    : TransactionType.MobileTransfer,
                CreatedAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                SenderWalletId = vm.SenderWalletId,
                ReceiverWalletId = receiverWallet?.Id,
                SenderCurrencyId = senderWallet.CurrencyId,
                ReceiverCurrencyId = receiverWallet?.CurrencyId
                    ?? senderWallet.CurrencyId,
                ReceiverPhoneNumber = vm.ReceiverPhoneNumber,
                ReceiverName = vm.ReceiverName
            };

            await _transactionRepository.AddAsync(transaction);

            senderWallet.Balance -= (vm.Amount + feeAmount);
            await _walletRepository.UpdateAsync(senderWallet);

            if (receiverWallet != null)
            {
                receiverWallet.Balance += convertedAmount;
                await _walletRepository.UpdateAsync(receiverWallet);

                await _notificationRepository.AddAsync(new Notification
                {
                    Title = "Money Received!",
                    Message = $"You received {convertedAmount:F2} " +
                              $"{receiverWallet.Currency?.Symbol}",
                    Type = NotificationType.TransactionReceived,
                    UserId = receiverWallet.UserId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                });
            }

            await _notificationRepository.AddAsync(new Notification
            {
                Title = "Transfer Sent!",
                Message = $"Your transfer of {vm.Amount:F2} " +
                          $"{senderWallet.Currency?.Symbol} was successful." +
                          (isFreeTransaction ? " (Fee Waived! 🎉)" : ""),
                Type = NotificationType.TransactionSent,
                UserId = userId,
                CreatedAt = DateTime.Now,
                IsRead = false
            });

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.TransactionCount++;
                await _userManager.UpdateAsync(user);
            }

            TempData["Success"] = isFreeTransaction
                ? "Transfer successful! This transaction was fee-free! 🎉"
                : "Transfer successful!";

            return RedirectToAction("Details", "Transaction",
                new { id = transaction.Id });
        }

        private async Task<TransferViewModel> RepopulateTransferVM(TransferViewModel vm, string userId)
        {
            var wallets = await _walletRepository.GetByUserIdAsync(userId);
            var beneficiaries = await _beneficiaryRepository.GetByUserIdAsync(userId);

            vm.UserWallets = wallets.Select(w => new WalletSummaryViewModel
            {
                Id = w.Id,
                SerialNumber = w.SerialNumber,
                Balance = w.Balance,
                CurrencyCode = w.Currency?.Code ?? "",
                CurrencySymbol = w.Currency?.Symbol ?? "",
                IsDefault = w.IsDefault
            }).ToList();

            vm.Beneficiaries = beneficiaries.Select(b => new BeneficiarySelectViewModel
            {
                Id = b.Id,
                Nickname = b.Nickname,
                ReceiverName = b.ReceiverName,
                ReceiverWalletSerial = b.ReceiverWalletSerial,
                Type = b.Type.ToString()
            }).ToList();

            return vm;
        }

        private string GenerateSerial(string prefix)
        {
            var year = DateTime.Now.Year;
            var random = new Random().Next(10000, 99999);
            return $"{prefix}-{year}-{random}";
        }
    }
}