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
    public class TransactionApiController : ControllerBase
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly UserManager<User> _userManager;

        public TransactionApiController(
            ITransactionRepository transactionRepository,
            UserManager<User> userManager)
        {
            _transactionRepository = transactionRepository;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyTransactions()
        {
            var userId = _userManager.GetUserId(User);

            var sent = await _transactionRepository.GetSentByUserAsync(userId);
            var received = await _transactionRepository.GetReceivedByUserAsync(userId);

            var all = sent.Select(t => new
            {
                t.Id,
                t.SerialNumber,
                t.Amount,
                t.ConvertedAmount,
                t.FeeAmount,
                t.FeeWaived,
                t.Description,
                Status = t.Status.ToString(),
                Type = t.Type.ToString(),
                t.CreatedAt,
                IsSent = true,
                ReceiverName = t.ReceiverWallet?.User != null
                    ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}"
                    : t.ReceiverName,
                SenderCurrency = t.SenderCurrency?.Code,
                ReceiverCurrency = t.ReceiverCurrency?.Code
            })
            .Union(received.Select(t => new
            {
                t.Id,
                t.SerialNumber,
                t.Amount,
                t.ConvertedAmount,
                t.FeeAmount,
                t.FeeWaived,
                t.Description,
                Status = t.Status.ToString(),
                Type = t.Type.ToString(),
                t.CreatedAt,
                IsSent = false,
                ReceiverName = t.ReceiverWallet?.User != null
                    ? $"{t.ReceiverWallet.User.FirstName} {t.ReceiverWallet.User.LastName}"
                    : t.ReceiverName,
                SenderCurrency = t.SenderCurrency?.Code,
                ReceiverCurrency = t.ReceiverCurrency?.Code
            }))
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

            return Ok(all);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            var transaction = await _transactionRepository
                .GetTransactionWithDetailsAsync(id);

            if (transaction == null)
                return NotFound(new { message = "Transaction not found." });

            return Ok(new
            {
                transaction.Id,
                transaction.SerialNumber,
                transaction.Amount,
                transaction.ConvertedAmount,
                transaction.FeeAmount,
                transaction.FeeWaived,
                transaction.ExchangeRateUsed,
                transaction.Description,
                Status = transaction.Status.ToString(),
                Type = transaction.Type.ToString(),
                transaction.CreatedAt,
                transaction.CompletedAt,
                SenderName = $"{transaction.SenderWallet?.User?.FirstName} " +
                             $"{transaction.SenderWallet?.User?.LastName}",
                ReceiverName = transaction.ReceiverWallet?.User != null
                    ? $"{transaction.ReceiverWallet.User.FirstName} " +
                      $"{transaction.ReceiverWallet.User.LastName}"
                    : transaction.ReceiverName,
                SenderCurrency = transaction.SenderCurrency?.Code,
                ReceiverCurrency = transaction.ReceiverCurrency?.Code
            });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = _userManager.GetUserId(User);

            var totalSent = await _transactionRepository
                .GetTotalSentByUserAsync(userId);
            var totalReceived = await _transactionRepository
                .GetTotalReceivedByUserAsync(userId);
            var count = await _transactionRepository
                .GetUserTransactionCountAsync(userId);

            return Ok(new
            {
                TotalSent = totalSent,
                TotalReceived = totalReceived,
                TransactionCount = count,
                NextFreeAt = count % 10 == 0
                    ? "Next transaction is FREE! 🎉"
                    : $"{10 - (count % 10)} transactions until next free"
            });
        }
    }
}