using MoneyTransfer.Models;
using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.ViewModels
{
    public class CreateWalletViewModel
    {
        [Required]
        public int CurrencyId { get; set; }
        public string? Description { get; set; }
        public List<CurrencySelectViewModel> AvailableCurrencies { get; set; } = new List<CurrencySelectViewModel>();
    }
}
