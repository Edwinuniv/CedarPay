namespace MoneyTransfer.ViewModels
{
    public class TransactionViewModel
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal FeeAmount { get; set; }
        public bool FeeWaived { get; set; }
        public decimal ExchangeRateUsed { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsSent { get; set; }

        public string OtherPartyName { get; set; } = "";
        public string? OtherPartyPictureUrl { get; set; }
        public string OtherPartyInitials { get; set; } = "";

        public string ReceiverName { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string? ReceiverPictureUrl { get; set; }

        public string CurrencySymbol { get; set; } = "$";
        public string SenderCurrency { get; set; } = "";
        public string ReceiverCurrency { get; set; } = "";

        public string SenderWalletSerial { get; set; } = "";
        public string ReceiverWalletSerial { get; set; } = "";
    }
}