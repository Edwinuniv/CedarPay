namespace MoneyTransfer.Services.Interfaces
{

    public interface ICurrencyExchangeService
    {
        Task<decimal> GetRateAsync(string from, string to);
    }

}
