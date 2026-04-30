using Microsoft.Extensions.Configuration;
using System.Text.Json;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Services.Implementations
{
    public class CurrencyExchangeService: ICurrencyExchangeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private static readonly Dictionary<string, (decimal rate, DateTime cachedAt)> _cache = new Dictionary<string, (decimal rate, DateTime cachedAt)>();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

        public CurrencyExchangeService(IHttpClientFactory factory, IConfiguration config)
        {
            _httpClient = factory.CreateClient();
            _baseUrl = config["ExchangeRateApi:BaseUrl"] ?? "https://open.er-api.com/v6/latest/";
        }

        public async Task<decimal> GetRateAsync(string from, string to)
        {
            if (from == to)
            {
                return 1m;
            }
            string key = $"{from}_{to}";

            if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.cachedAt < CacheDuration)
            {
                return cached.rate;
            }
            try
            {
                var url = $"{_baseUrl}{from}";
                var json = await _httpClient
                    .GetStringAsync(url);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("rates", out var rates) && rates.TryGetProperty(to, out var rEl))
                {
                    var rate = (decimal)rEl.GetDouble();
                    _cache[key] = (rate, DateTime.UtcNow);
                    return rate;
                }
            }
            catch
            { 
            
            }

            return GetFallbackRate(from, to);
        }

        private static decimal GetFallbackRate(string from, string to)
        {
            var toUsd = new Dictionary<string, decimal>
            {
                ["USD"] = 1m,
                ["EUR"] = 1.08m,
                ["LBP"] = 0.0000112m,
                ["AED"] = 0.272m
            };

            if (!toUsd.TryGetValue(from, out var f)) 
            {
                f = 1m;
            }
            if (!toUsd.TryGetValue(to, out var t)) 
            {
                t = 1m; 
            }
            if (t == 0)
            {
                return 1m;
            }
            return f / t;
        }
    }
}