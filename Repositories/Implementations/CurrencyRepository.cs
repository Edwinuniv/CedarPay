using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Repositories.Implementations
{
    public class CurrencyRepository : GenericRepository<Currency>, ICurrencyRepository
    {
        public CurrencyRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Currency?> GetByCodeAsync(string code)
        {
            return await _context.Currencies.FirstOrDefaultAsync(c => c.Code == code);
        }

        public async Task<IEnumerable<Currency>> GetActiveCurrenciesAsync()
        {
            return await _context.Currencies.Where(c => c.IsActive).ToListAsync();
        }

        public async Task<decimal> GetExchangeRateAsync(string fromCode, string toCode)
        {
            if (fromCode == toCode) return 1m;

            var from = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == fromCode && c.IsActive);
            var to = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == toCode && c.IsActive);

            if (from == null || to == null) return 1m;
            if (to.ExchangeRateToUSD == 0) return 1m;

            return from.ExchangeRateToUSD / to.ExchangeRateToUSD;
        }
    }
}