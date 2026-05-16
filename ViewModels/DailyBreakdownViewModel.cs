namespace MoneyTransfer.ViewModels
{
    public class DailyBreakdownViewModel
    {
        public DateTime Date { get; set; }
        public int Day { get; set; }
        public int Transactions { get; set; }
        public decimal Volume { get; set; }
        public int Count { get; set; }
        public decimal Fees { get; set; }
        public int NewUsers { get; set; }
        public int Reviews { get; set; }
    }
}
