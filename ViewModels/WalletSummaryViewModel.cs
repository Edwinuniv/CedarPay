namespace MoneyTransfer.ViewModels
{
    public class WalletSummaryViewModel
    {
        public int Id { get; set; }
        public string CurrencyCode { get; set; } = "";
        public string CurrencySymbol { get; set; } = "";
        public string CurrencyName { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsDefault { get; set; }
        public string SerialNumber { get; set; } = "";
    }
}
