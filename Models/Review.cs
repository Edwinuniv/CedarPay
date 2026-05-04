using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum ReviewTarget
    {
        App,
        Agent,
        Transaction  
    }

    public class Review
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public ReviewTarget Target { get; set; }
        public DateTime CreatedAt { get; set; }

        [Required]
        public string UserId { get; set; } = "";
        public int? AgentId { get; set; }
        public int? TransactionId { get; set; }

        public User User { get; set; } = null!;
        public Agent? Agent { get; set; }
        public Transaction? Transaction { get; set; }
    }
}