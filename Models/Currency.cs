using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class Currency
    {
        public int Id { get; set; }
        [Required]
        public string Code { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Symbol { get; set; }
        public string? FlagUrl { get; set; }
        public decimal ExchangeRateToUSD { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUpdated { get; set; }

        public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
        public ICollection<TopUp> TopUps { get; set; } = new List<TopUp>();
    }
}
