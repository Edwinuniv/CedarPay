using MoneyTransfer.Models;

namespace MoneyTransfer.Repositories.Interfaces
{
    public interface ICurrencyRepository: IGenericRepository<Currency>
    {
        Task<Currency?> GetByCodeAsync(string code);
        Task<IEnumerable<Currency>> GetActiveCurrenciesAsync();
        Task<decimal> GetExchangeRateAsync(string fromCode, string toCode);
    }
}
