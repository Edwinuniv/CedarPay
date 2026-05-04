namespace MoneyTransfer.ViewModels
{
    public class DailyBreakdownViewModel
    {
        public DateTime Date { get; set; }
        public int Transactions { get; set; }
        public decimal Volume { get; set; }
        public int NewUsers { get; set; }
        public int Reviews { get; set; }
    }
}
