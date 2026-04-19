using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public string Content { get; set; } = "";

        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }

        public string? SenderId { get; set; }
        public User? Sender { get; set; }

        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; } = null!;

        public bool IsFromAdmin { get; set; }
    }
}