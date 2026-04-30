using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using MoneyTransfer.Data;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class WalletController : BaseController
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public WalletController(
            IWalletRepository walletRepository,
            IAccountRepository accountRepository,
            ICurrencyRepository currencyRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            ApplicationDbContext context,
            IEmailService emailService) : base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _accountRepository = accountRepository;
            _currencyRepository = currencyRepository;
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            var vm = wallets.Select(w => new WalletViewModel
            {
                Id = w.Id,
                SerialNumber = w.SerialNumber,
                Balance = w.Balance,
                Description = w.Description,
                IsDefault = w.IsDefault,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                CurrencyCode = w.Currency?.Code ?? "",
                CurrencySymbol = w.Currency?.Symbol ?? "",
                CurrencyName = w.Currency?.Name ?? "",
                FlagUrl = w.Currency?.FlagUrl
            }).ToList();

            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            var currencies = await _currencyRepository.GetActiveCurrenciesAsync();

            var vm = new CreateWalletViewModel
            {
                AvailableCurrencies = currencies.Select(c => new CurrencySelectViewModel
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    Symbol = c.Symbol,
                    FlagUrl = c.FlagUrl
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Store(CreateWalletViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var currencies = await _currencyRepository
                    .GetActiveCurrenciesAsync();
                vm.AvailableCurrencies = currencies
                    .Select(c => new CurrencySelectViewModel
                    {
                        Id = c.Id,
                        Code = c.Code,
                        Name = c.Name,
                        Symbol = c.Symbol
                    }).ToList();
                return View("Create", vm);
            }

            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null || !user.ProfileCompleted)
            {
                TempData["Error"] =
                    "Please complete your profile before " +
                    "requesting a wallet.";
                return RedirectToAction("Profile", "Account");
            }

            if (User.IsInRole(Roles.Admin))
            {
                var account = await _accountRepository
                    .GetDefaultAccountAsync(userId);
                if (account == null)
                {
                    account = new Account
                    {
                        SerialNumber = GenerateSerial("ACC"),
                        UserId = userId,
                        IsActive = true,
                        IsDefault = true,
                        CreatedAt = DateTime.Now
                    };
                    await _accountRepository.AddAsync(account);
                }

                var existingWallets = await _walletRepository
                    .GetByUserIdAsync(userId);
                var isFirst = !existingWallets.Any();

                var wallet = new Wallet
                {
                    SerialNumber = GenerateSerial("WAL"),
                    Balance = 0,
                    Description = vm.Description,
                    IsActive = true,
                    IsDefault = isFirst,
                    CreatedAt = DateTime.Now,
                    UserId = userId,
                    AccountId = account.Id,
                    CurrencyId = vm.CurrencyId
                };

                await _walletRepository.AddAsync(wallet);

                // Send email notification for wallet creation
                if (user?.Email != null)
                {
                    var currency = await _currencyRepository.GetByIdAsync(vm.CurrencyId);
                    _ = Task.Run(async () =>
                    {
                        await _emailService.SendNotificationAsync(
                            user.Email,
                            $"{user.FirstName} {user.LastName}",
                            "Wallet Created Successfully",
                            $"A new {currency?.Code} wallet has been created for you.\n\n" +
                            $"Wallet Serial: {wallet.SerialNumber}\n" +
                            $"Currency: {currency?.Code} - {currency?.Name}\n" +
                            $"Created on: {DateTime.Now:MMMM dd, yyyy}\n\n" +
                            $"You can now use this wallet to send and receive money.");
                    });
                }

                TempData["Success"] = "Wallet created!";
                return RedirectToAction("Index");
            }

            var userWallets = await _walletRepository
                .GetByUserIdAsync(userId);
            if (userWallets.Any(w => w.CurrencyId == vm.CurrencyId))
            {
                ModelState.AddModelError("",
                    "You already have a wallet " +
                    "in this currency.");
                var currencies = await _currencyRepository
                    .GetActiveCurrenciesAsync();
                vm.AvailableCurrencies = currencies
                    .Select(c => new CurrencySelectViewModel
                    {
                        Id = c.Id,
                        Code = c.Code,
                        Name = c.Name,
                        Symbol = c.Symbol
                    }).ToList();
                return View("Create", vm);
            }

            var pendingRequest = await _context.WalletRequests
                .AnyAsync(r => r.UserId == userId
                            && r.CurrencyId == vm.CurrencyId
                            && r.Status == WalletRequestStatus.Pending);

            if (pendingRequest)
            {
                TempData["Error"] =
                    "You already have a pending wallet " +
                    "request for this currency. " +
                    "Please wait for admin approval.";
                return RedirectToAction("MyRequests");
            }

            var currencyForRequest = await _currencyRepository.GetByIdAsync(vm.CurrencyId);

            await _context.WalletRequests.AddAsync(new WalletRequest
            {
                UserId = userId,
                CurrencyId = vm.CurrencyId,
                Description = vm.Description,
                Status = WalletRequestStatus.Pending,
                RequestedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            // Send email notification for wallet request
            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Wallet Request Submitted",
                        $"Your request for a {currencyForRequest?.Code} wallet has been submitted for review.\n\n" +
                        $"Currency: {currencyForRequest?.Code} - {currencyForRequest?.Name}\n" +
                        $"Requested on: {DateTime.Now:MMMM dd, yyyy}\n\n" +
                        $"You will be notified once an admin approves your request.");
                });
            }

            TempData["Success"] =
                "Wallet request submitted! " +
                "An admin will review it shortly.";
            return RedirectToAction("MyRequests");
        }

        [HttpPost]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            foreach (var w in wallets)
            {
                w.IsDefault = (w.Id == id);
                await _walletRepository.UpdateAsync(w);
            }

            TempData["Success"] = "Default wallet updated!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var wallet = await _walletRepository.GetByIdAsync(id);
            if (wallet == null) return NotFound();

            if (wallet.Balance > 0)
            {
                TempData["Error"] = "Cannot delete a wallet with balance!";
                return RedirectToAction("Index");
            }

            await _walletRepository.DeleteAsync(id);
            TempData["Success"] = "Wallet deleted!";
            return RedirectToAction("Index");
        }

        private string GenerateSerial(string prefix)
        {
            var year = DateTime.Now.Year;
            var random = new Random().Next(10000, 99999);
            return $"{prefix}-{year}-{random}";
        }

        public async Task<IActionResult> MyRequests()
        {
            var userId = _userManager.GetUserId(User);
            var requests = await _context.WalletRequests
                .Include(r => r.Currency)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(requests);
        }
    }
}