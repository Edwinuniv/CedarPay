namespace MoneyTransfer.ViewModels
{
    public class TopUpSummaryViewModel
    {
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CurrencySymbol { get; set; }
    }
}
