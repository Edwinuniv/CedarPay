namespace MoneyTransfer.ViewModels
{
    public class RecentTransactionViewModel
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string ReceiverInitials { get; set; } = "";
        public string? ReceiverPictureUrl { get; set; }
        public decimal Amount { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public string Status { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsSent { get; set; }
    }

}
