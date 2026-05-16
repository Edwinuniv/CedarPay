using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletApiController : ControllerBase
    {
        private readonly IWalletRepository _walletRepository;
        private readonly UserManager<User> _userManager;

        public WalletApiController(IWalletRepository walletRepository, UserManager<User> userManager)
        {
            _walletRepository = walletRepository;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyWallets()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            var result = wallets.Select(w => new
            {
                w.Id,
                w.SerialNumber,
                w.Balance,
                w.Description,
                w.IsDefault,
                w.IsActive,
                w.CreatedAt,
                Currency = new
                {
                    w.Currency?.Code,
                    w.Currency?.Symbol,
                    w.Currency?.Name
                }
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetWallet(int id)
        {
            var wallet = await _walletRepository.GetByIdAsync(id);

            if (wallet == null)
                return NotFound(new { message = "Wallet not found." });

            var userId = _userManager.GetUserId(User);
            if (wallet.UserId != userId)
                return Unauthorized(new { message = "Access denied." });

            return Ok(new
            {
                wallet.Id,
                wallet.SerialNumber,
                wallet.Balance,
                wallet.IsDefault,
                Currency = new
                {
                    wallet.Currency?.Code,
                    wallet.Currency?.Symbol,
                    wallet.Currency?.Name
                }
            });
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetTotalBalance()
        {
            var userId = _userManager.GetUserId(User);
            var wallets = await _walletRepository.GetByUserIdAsync(userId);

            var total = wallets.Sum(w => w.Balance);
            var defaultWallet = wallets.FirstOrDefault(w => w.IsDefault);

            return Ok(new
            {
                TotalBalance = total,
                PrimaryCurrency = defaultWallet?.Currency?.Code ?? "USD",
                Symbol = defaultWallet?.Currency?.Symbol ?? "$",
                WalletCount = wallets.Count()
            });
        }
    }
}