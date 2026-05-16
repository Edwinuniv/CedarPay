namespace MoneyTransfer.ViewModels
{
    public class MonthlyStatViewModel
    {
        public string Month { get; set; }
        public int Transactions { get; set; }
        public decimal Volume { get; set; }
        public int NewUsers { get; set; }
        public int Reviews { get; set; }
        public double AverageRating { get; set; }
    }
}
