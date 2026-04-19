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
    public class BeneficiaryController : BaseController
    {
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public BeneficiaryController(IBeneficiaryRepository beneficiaryRepository,
            IWalletRepository walletRepository,
            UserManager<User> userManager,
            IUserRepository userRepository,
            ApplicationDbContext context) : base(userManager, userRepository)
        {
            _beneficiaryRepository = beneficiaryRepository;
            _walletRepository = walletRepository;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var beneficiaries = await _beneficiaryRepository.GetByUserIdAsync(userId);

            var vm = beneficiaries.Select(b => new BeneficiaryViewModel
            {
                Id = b.Id,
                Nickname = b.Nickname,
                ReceiverName = b.ReceiverName,
                ReceiverPhoneNumber = b.ReceiverPhoneNumber,
                ReceiverWalletSerial = b.ReceiverWalletSerial,
                ReceiverCountry = b.ReceiverCountry,
                ReceiverCity = b.ReceiverCity,
                ReceiverBankName = b.ReceiverBankName,
                TransferType = b.Type.ToString(),
                CreatedAt = b.CreatedAt
            }).ToList();

            return View(vm);
        }

        public IActionResult Create()
        {
            return View(new BeneficiaryViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Store(BeneficiaryViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("Create", vm);

            var userId = _userManager.GetUserId(User);

            if (!string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                var receiverWallet = await _walletRepository
                    .GetBySerialNumberAsync(vm.ReceiverWalletSerial);

                if (receiverWallet != null)
                {
                    if (receiverWallet.UserId == userId)
                    {
                        var myWallets = (await _walletRepository
                            .GetByUserIdAsync(userId)).ToList();

                        bool hasSameCurrencyOwnWallet = false;
                        foreach (var w in myWallets)
                        {
                            if (w.CurrencyId == receiverWallet.CurrencyId
                                && w.Id != receiverWallet.Id)
                            {
                                hasSameCurrencyOwnWallet = true;
                                break;
                            }
                        }

                        if (hasSameCurrencyOwnWallet ||
                            receiverWallet.CurrencyId ==
                            myWallets.FirstOrDefault(
                                w => w.IsDefault)?.CurrencyId)
                        {
                            ModelState.AddModelError(
                                "ReceiverWalletSerial",
                                "You cannot add your own wallet " +
                                "of the same currency as a beneficiary. " +
                                "You can add a different currency wallet.");
                            return View("Create", vm);
                        }
                    }
                }
            }

            var beneficiary = new Beneficiary
            {
                Nickname = vm.Nickname,
                ReceiverName = vm.ReceiverName,
                ReceiverPhoneNumber = vm.ReceiverPhoneNumber,
                ReceiverWalletSerial = vm.ReceiverWalletSerial,
                ReceiverCountry = vm.ReceiverCountry,
                ReceiverCity = vm.ReceiverCity,
                ReceiverBankName = vm.ReceiverBankName,
                Type = vm.TransferType == "WalletTransfer"
                    ? BeneficiaryType.WalletTransfer
                    : BeneficiaryType.MobileTransfer,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            await _beneficiaryRepository.AddAsync(beneficiary);
            TempData["Success"] = "Beneficiary added!";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(int id)
        {
            var beneficiary = await _beneficiaryRepository.GetByIdAsync(id);
            if (beneficiary == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (beneficiary.UserId != userId)
                return Forbid();

            var vm = new BeneficiaryViewModel
            {
                Id = beneficiary.Id,
                Nickname = beneficiary.Nickname,
                ReceiverName = beneficiary.ReceiverName,
                ReceiverPhoneNumber = beneficiary.ReceiverPhoneNumber,
                ReceiverWalletSerial = beneficiary.ReceiverWalletSerial,
                ReceiverCountry = beneficiary.ReceiverCountry,
                ReceiverCity = beneficiary.ReceiverCity,
                ReceiverBankName = beneficiary.ReceiverBankName,
                TransferType = beneficiary.Type.ToString()
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Update(BeneficiaryViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("Edit", vm);

            var beneficiary = await _beneficiaryRepository.GetByIdAsync(vm.Id);
            if (beneficiary == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (beneficiary.UserId != userId)
                return Forbid();

            if (!string.IsNullOrEmpty(vm.ReceiverWalletSerial))
            {
                var receiverWallet = await _walletRepository
                    .GetBySerialNumberAsync(vm.ReceiverWalletSerial);

                if (receiverWallet != null && receiverWallet.UserId == userId)
                {
                    var myWallets = await _walletRepository.GetByUserIdAsync(userId);

                    bool hasSameCurrencyWallet = false;
                    foreach (var wallet in myWallets)
                    {
                        if (wallet.CurrencyId == receiverWallet.CurrencyId && wallet.Id != receiverWallet.Id)
                        {
                            hasSameCurrencyWallet = true;
                            break;
                        }
                    }

                    if (hasSameCurrencyWallet)
                    {
                        ModelState.AddModelError("ReceiverWalletSerial",
                            "You cannot add your own wallet of the same currency as a beneficiary.");
                        return View("Edit", vm);
                    }
                }
            }

            beneficiary.Nickname = vm.Nickname;
            beneficiary.ReceiverName = vm.ReceiverName;
            beneficiary.ReceiverPhoneNumber = vm.ReceiverPhoneNumber;
            beneficiary.ReceiverWalletSerial = vm.ReceiverWalletSerial;
            beneficiary.ReceiverCountry = vm.ReceiverCountry;
            beneficiary.ReceiverCity = vm.ReceiverCity;
            beneficiary.ReceiverBankName = vm.ReceiverBankName;
            beneficiary.Type = vm.TransferType == "WalletTransfer"
                ? BeneficiaryType.WalletTransfer
                : BeneficiaryType.MobileTransfer;

            await _beneficiaryRepository.UpdateAsync(beneficiary);
            TempData["Success"] = "Beneficiary updated successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var beneficiary = await _beneficiaryRepository.GetByIdAsync(id);
            if (beneficiary == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (beneficiary.UserId != userId)
                return Forbid();

            await _beneficiaryRepository.DeleteAsync(id);
            TempData["Success"] = "Beneficiary removed successfully!";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Json(new List<object>());

            var lower = username.ToLower().TrimStart('@');
            var currentUserId = _userManager.GetUserId(User);

            var users = await _context.Users
                .Include(u => u.Wallets)
                    .ThenInclude(w => w.Currency)
                .Where(u => u.Id != currentUserId &&
                            u.UserName != null &&
                            u.UserName.Contains(lower))
                .Take(5)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.FirstName + " " + u.LastName,
                    userName = u.UserName ?? "",
                    initials = (u.FirstName != null
                        ? u.FirstName.Substring(0, 1) : "?") +
                        (u.LastName != null
                        ? u.LastName.Substring(0, 1) : "?"),
                    walletSerial = u.Wallets
                        .Where(w => w.IsDefault && w.IsActive)
                        .Select(w => w.SerialNumber)
                        .FirstOrDefault() ?? ""
                })
                .ToListAsync();

            return Json(users);
        }
    }
}