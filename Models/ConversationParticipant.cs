using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public class ConversationParticipant
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        [Required]
        public string UserId { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsAdmin { get; set; }

        public Conversation Conversation { get; set; }
        public User User { get; set; }
    }
}