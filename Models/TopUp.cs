namespace MoneyTransfer.Models
{
    public enum TopUpStatus
    {
        Pending,
        Completed,
        Failed
    }
    public enum TopUpMethod
    {
        CreditCard,
        DebitCard,
        BankTransfer,
        Cash
    }
    public class TopUp
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public TopUpStatus Status { get; set; }
        public TopUpMethod Method { get; set; }
        public string? PaymentReference { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public int WalletId { get; set; }
        public int CurrencyId { get; set; }

        public Wallet Wallet { get; set; }
        public Currency Currency { get; set; }
    }
}
