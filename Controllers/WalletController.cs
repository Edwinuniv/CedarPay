using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Constants;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using MoneyTransfer.Data;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class WalletController : BaseController
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public WalletController(IWalletRepository walletRepository, IAccountRepository accountRepository,
            ICurrencyRepository currencyRepository, UserManager<User> userManager,
            IUserRepository userRepository, ApplicationDbContext context) : base(userManager, userRepository)
        {
            _walletRepository = walletRepository;
            _accountRepository = accountRepository;
            _currencyRepository = currencyRepository;
            _userManager = userManager;
            _context = context;
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

            await _context.WalletRequests.AddAsync(new WalletRequest
            {
                UserId = userId,
                CurrencyId = vm.CurrencyId,
                Description = vm.Description,
                Status = WalletRequestStatus.Pending,
                RequestedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

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
    }
}