namespace MoneyTransfer.ViewModels
{
    public class MonthlyFinanceViewModel
    {
        public string Month { get; set; } = "";
        public decimal TotalSent { get; set; }
        public decimal TotalReceived { get; set; }
    }
}
