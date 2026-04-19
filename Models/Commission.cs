namespace MoneyTransfer.Models
{
    public class Commission
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        public bool IsPaid { get; set; }
        public DateTime EarnedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public int AgentId { get; set; }
        public int TransactionId { get; set; }
        
        public Agent Agent { get; set; }
        public Transaction Transaction { get; set; }    
    }
}
