using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Constants;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;
using MoneyTransfer.ViewModels;
using Microsoft.EntityFrameworkCore;

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
        private readonly ICurrencyExchangeService _exchangeService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public TransferController(IWalletRepository walletRepository, ITransactionRepository transactionRepository, IBeneficiaryRepository beneficiaryRepository, ICurrencyRepository currencyRepository, INotificationRepository notificationRepository, IAccountRepository accountRepository, UserManager<User> userManager, IUserRepository userRepository, ICurrencyExchangeService exchangeService, IEmailService emailService, ApplicationDbContext context, IServiceProvider serviceProvider): base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _transactionRepository = transactionRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _currencyRepository = currencyRepository;
            _notificationRepository = notificationRepository;
            _accountRepository = accountRepository;
            _exchangeService = exchangeService;
            _emailService = emailService;
            _context = context;
            _serviceProvider = serviceProvider;
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);
            var beneficiaries = await _beneficiaryRepository.GetByUserIdAsync(userId);

            var feePolicy = await _context.FeePolicies.AsNoTracking().FirstOrDefaultAsync() ?? new FeePolicy { FeePercentage = 0.02m, FixedFee = 0.50m, FreeTransactionThreshold = 10 };

            var txCount = await _transactionRepository.GetUserTransactionCountAsync(userId);
            bool nextIsFree = (txCount + 1) % feePolicy.FreeTransactionThreshold == 0;

            ViewBag.FeePercentage = feePolicy.FeePercentage * 100;
            ViewBag.FixedFee = feePolicy.FixedFee;
            ViewBag.NextIsFree = nextIsFree;
            ViewBag.FreeThreshold = feePolicy.FreeTransactionThreshold;
            ViewBag.TxCount = txCount;

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

            if (vm.TransferType == "WalletToWallet" && !string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                var receiverCheck = await _walletRepository.GetBySerialNumberAsync(vm.ReceiverWalletSerial);
                if (receiverCheck != null && receiverCheck.UserId == userId)
                {
                    if (receiverCheck.CurrencyId == senderWallet.CurrencyId)
                    {
                        ModelState.AddModelError("", "You cannot send money to a wallet of the same currency. Use a different currency wallet.");
                        return View("Create", await RepopulateTransferVM(vm, userId));
                    }
                }
            }

            if (vm.TransferType == "MobileTransfer")
            {
                var currentUser = await _userManager.FindByIdAsync(userId);
                if (currentUser != null && !string.IsNullOrEmpty(vm.ReceiverPhoneNumber) && currentUser.PhoneNumber == vm.ReceiverPhoneNumber)
                {
                    ModelState.AddModelError("", "You cannot send money to your own phone number.");
                    return View("Create", await RepopulateTransferVM(vm, userId));
                }
            }

            var hasFunds = await _walletRepository.HasSufficientBalenceAsync(vm.SenderWalletId, vm.Amount);
            if (!hasFunds)
            {
                ModelState.AddModelError("", "Insufficient balance.");
                return View("Create", await RepopulateTransferVM(vm, userId));
            }

            var feePolicy = await _context.FeePolicies.AsNoTracking().FirstOrDefaultAsync()
                ?? new FeePolicy { FeePercentage = 0.02m, FixedFee = 0.50m, FreeTransactionThreshold = 10 };

            decimal feeAmount = 0;
            bool feeWaived = false;

            var txCount = await _transactionRepository.GetUserTransactionCountAsync(userId);

            if ((txCount + 1) % feePolicy.FreeTransactionThreshold == 0)
            {
                feeWaived = true;
            }
            else
            {
                feeAmount = (vm.Amount * feePolicy.FeePercentage) + feePolicy.FixedFee;
            }

            Wallet? receiverWallet = null;
            if (vm.TransferType == "WalletToWallet" && !string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                receiverWallet = await _walletRepository.GetBySerialNumberAsync(vm.ReceiverWalletSerial);
                if (receiverWallet == null)
                {
                    ModelState.AddModelError("", "Receiver wallet not found.");
                    return View("Create", await RepopulateTransferVM(vm, userId));
                }
            }

            var exchangeRate = 1m;
            var convertedAmount = vm.Amount;

            if (receiverWallet != null && senderWallet.CurrencyId != receiverWallet.CurrencyId)
            {
                var senderCurrency = senderWallet.Currency ?? await _currencyRepository.GetByIdAsync(senderWallet.CurrencyId);
                var receiverCurrency = receiverWallet.Currency ?? await _currencyRepository.GetByIdAsync(receiverWallet.CurrencyId);

                if (senderCurrency != null && receiverCurrency != null)
                {
                    decimal rate;
                    if (senderCurrency.Code == receiverCurrency.Code)
                    {
                        rate = 1m;
                    }
                    else
                    {
                        rate = await _exchangeService.GetRateAsync(senderCurrency.Code, receiverCurrency.Code);
                        if (rate == 1m && senderCurrency.Code != receiverCurrency.Code)
                        {
                            rate = await _currencyRepository.GetExchangeRateAsync(senderCurrency.Code, receiverCurrency.Code);
                        }
                    }

                    exchangeRate = rate;
                    convertedAmount = vm.Amount * rate;
                    senderWallet.Currency = senderCurrency;
                    receiverWallet.Currency = receiverCurrency;
                }
            }

            var transaction = new Transaction
            {
                SerialNumber = GenerateSerial("TXN"),
                Amount = vm.Amount,
                ConvertedAmount = convertedAmount,
                FeeAmount = feeAmount,
                FeeWaived = feeWaived,
                ExchangeRateUsed = exchangeRate,
                Description = vm.Description,
                Status = TransactionStatus.Completed,
                Type = vm.TransferType == "WalletToWallet" ? TransactionType.WalletToWallet : TransactionType.MobileTransfer,
                Category = vm.Category,
                CreatedAt = DateTime.Now,
                CompletedAt = DateTime.Now,
                SenderWalletId = vm.SenderWalletId,
                ReceiverWalletId = receiverWallet?.Id,
                SenderCurrencyId = senderWallet.CurrencyId,
                ReceiverCurrencyId = receiverWallet?.CurrencyId ?? senderWallet.CurrencyId,
                ReceiverPhoneNumber = vm.ReceiverPhoneNumber,
                ReceiverName = vm.ReceiverName
            };

            await _transactionRepository.AddAsync(transaction);

            senderWallet.Balance -= (vm.Amount + feeAmount);
            await _walletRepository.UpdateAsync(senderWallet);

            var senderUser = await _userManager.FindByIdAsync(userId);
            var receiverUser = receiverWallet != null ? await _userManager.FindByIdAsync(receiverWallet.UserId) : null;

            if (receiverWallet != null)
            {
                receiverWallet.Balance += convertedAmount;
                await _walletRepository.UpdateAsync(receiverWallet);

                var notification = new Notification
                {
                    Title = "Money Received!",
                    Message = $"You received {convertedAmount:F2} {receiverWallet.Currency?.Symbol}",
                    Type = NotificationType.TransactionReceived,
                    UserId = receiverWallet.UserId,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                await _notificationRepository.AddAsync(notification);

                if (receiverUser?.Email != null)
                {
                    var receiverCurrencySymbol = receiverWallet.Currency?.Symbol ?? "";
                    var senderName = $"{senderUser?.FirstName} {senderUser?.LastName}".Trim();

                    SendEmailAsync(receiverUser.Email, $"{receiverUser.FirstName} {receiverUser.LastName}", "Money Received!", $"You have received {receiverCurrencySymbol}{convertedAmount:N2} from {senderName}.\n\n" + $"Transaction ID: {transaction.SerialNumber}\n" + $"Amount: {receiverCurrencySymbol}{convertedAmount:N2}\n" + $"New balance: {receiverCurrencySymbol}{receiverWallet.Balance:N2}\n\n" + "Thank you for using CedarPay!");
                }
            }

            var sentNotification = new Notification
            {
                Title = "Transfer Sent!",
                Message = $"Your transfer of {vm.Amount:F2} {senderWallet.Currency?.Symbol} was successful." + (feeWaived ? " (Fee Waived! 🎉)" : ""),
                Type = NotificationType.TransactionSent,
                UserId = userId,
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            await _notificationRepository.AddAsync(sentNotification);

            if (senderUser?.Email != null)
            {
                var senderCurrencySymbol = senderWallet.Currency?.Symbol ?? "";
                var recipientLabel = receiverUser != null ? $"{receiverUser.FirstName} {receiverUser.LastName}".Trim() : vm.ReceiverName ?? vm.ReceiverPhoneNumber ?? "Recipient";

                SendReceiptEmailAsync(senderUser.Email, $"{senderUser.FirstName} {senderUser.LastName}", transaction.SerialNumber, vm.Amount, senderCurrencySymbol, senderWallet.Currency?.Code ?? "", recipientLabel, feeAmount, feeWaived, transaction.CreatedAt, vm.Description, vm.Category.ToString());
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.TransactionCount++;
                await _userManager.UpdateAsync(user);
            }

            TempData["Success"] = feeWaived ? "Transfer successful! This transaction was fee-free! 🎉" : "Transfer successful!";

            return RedirectToAction("Details", "Transaction", new { id = transaction.Id });
        }

        private void SendEmailAsync(string email, string name, string subject, string body)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendNotificationAsync(email, name, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Email send failed: {ex.Message}");
                }
            });
        }

        private void SendReceiptEmailAsync(string email, string name, string serial, decimal amount, string sym, string currency, string recipient, decimal fee, bool feeWaived, DateTime date, string? description, string category)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendTransactionReceiptAsync(email, name, serial, amount, sym, currency, recipient, fee, feeWaived, date, description, category);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Receipt email failed: {ex.Message}");
                }
            });
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