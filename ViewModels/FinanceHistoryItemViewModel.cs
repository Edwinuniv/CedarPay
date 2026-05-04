namespace MoneyTransfer.ViewModels
{
    public class FinanceHistoryItemViewModel
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public decimal Amount { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public string CurrencyCode { get; set; } = "";
        public string Description { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Fee { get; set; }
        public string Counterparty { get; set; } = "";
    }
}
