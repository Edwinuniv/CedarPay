using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CurrencyApiController : ControllerBase
    {
        private readonly ICurrencyRepository _currencyRepository;

        public CurrencyApiController(ICurrencyRepository currencyRepository)
        {
            _currencyRepository = currencyRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveCurrencies()
        {
            var currencies = await _currencyRepository.GetActiveCurrenciesAsync();

            return Ok(currencies.Select(c => new
            {
                c.Id,
                c.Code,
                c.Name,
                c.Symbol,
                c.ExchangeRateToUSD,
                c.LastUpdated
            }));
        }

        [HttpGet("rate")]
        public async Task<IActionResult> GetExchangeRate(
            [FromQuery] string from,
            [FromQuery] string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                return BadRequest(new { message = "from and to are required." });

            var rate = await _currencyRepository
                .GetExchangeRateAsync(from.ToUpper(), to.ToUpper());

            return Ok(new
            {
                From = from.ToUpper(),
                To = to.ToUpper(),
                Rate = rate,
                UpdatedAt = DateTime.Now
            });
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var currency = await _currencyRepository
                .GetByCodeAsync(code.ToUpper());

            if (currency == null)
                return NotFound(new { message = $"Currency {code} not found." });

            return Ok(new
            {
                currency.Id,
                currency.Code,
                currency.Name,
                currency.Symbol,
                currency.ExchangeRateToUSD,
                currency.IsActive,
                currency.LastUpdated
            });
        }
    }
}