using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum TicketStatus
    {
        Open,
        InProgress,
        Resolved,
        Closed
    }
    public enum TicketPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }
    public class SupportTicket
    {
        public int Id { get; set; }
        [Required]
        public string Subject { get; set; }
        [Required]
        public string Description { get; set; }
        public TicketStatus Status { get; set; }
        public TicketPriority Priority { get; set; }
        public string? AdminResponse { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        [Required]
        public string UserId { get; set; }
        
        public User User { get; set; }
    }
}
