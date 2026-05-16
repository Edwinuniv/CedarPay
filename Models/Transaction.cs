using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed,
        Cancelled,
        Refunded
    }

    public enum TransactionType
    {
        WalletToWallet,
        MobileTransfer,
        QRPayment,
        PaymentLink
    }

    public enum TransactionCategory
    {
        General,
        Bills,
        Rent,
        Shopping,
        Food,
        Healthcare,
        Education,
        Family,
        Business,
        Other
    }

    public class Transaction
    {
        public int Id { get; set; }
        [Required]
        public string SerialNumber { get; set; }
        public decimal Amount { get; set; }
        public decimal ConvertedAmount { get; set; }
        public decimal FeeAmount { get; set; }
        public bool FeeWaived { get; set; }
        public decimal ExchangeRateUsed { get; set; }
        public string? Description { get; set; }
        public TransactionStatus Status { get; set; }
        public TransactionType Type { get; set; }
        public TransactionCategory Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string? ReceiverPhoneNumber { get; set; }
        public string? ReceiverName { get; set; }

        public int SenderWalletId { get; set; }
        public int? ReceiverWalletId { get; set; }
        public int SenderCurrencyId { get; set; }
        public int? ReceiverCurrencyId { get; set; }

        public Wallet SenderWallet { get; set; }
        public Wallet? ReceiverWallet { get; set; }
        public Currency SenderCurrency { get; set; }
        public Currency ReceiverCurrency { get; set; }
        public Commission? Commission { get; set; }
    }
}