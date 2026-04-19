using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum ConversationType
    {
        UserToUser,
        UserToAgent,
        UserToAdmin,
        Group,
        Support
    }
    public class Conversation
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public ConversationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }

        [Required]
        public string UserId { get; set; }
        public string? AdminId { get; set; }

        public User User { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    }
}