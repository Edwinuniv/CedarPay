using System.ComponentModel.DataAnnotations;

namespace MoneyTransfer.Models
{
    public enum MessageType
    {
        Text,
        Image,
        Video,
        File,
        Voice
    }
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

        public int? ReplyToId { get; set; }
        public Message? ReplyTo { get; set; }

        public bool IsEdited { get; set; }
        public DateTime EditedAt { get; set; }

        public bool IsDeletedForSender { get; set; }
        public bool IsDeletedForEveryone { get; set; }

        public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();

        public MessageType MessageType { get; set; } = MessageType.Text;
        public string? MediaUrl { get; set; }
        public string? MediaFileName { get; set; }
        public long? MediaFileSize { get; set; }
    }
}