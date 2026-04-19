using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class WalletViewModel
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; }
        public decimal Balance { get; set; }
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySymbol { get; set; }
        public string CurrencyName { get; set; }
        public string? FlagUrl { get; set; }
    }
}
