namespace MoneyTransfer.Models
{
    public class MessageReaction
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public Message Message { get; set; } = null!;
        public string UserId { get; set; } = "";
        public User User = null!;
        public string Emoji { get; set; } = "";
        public DateTime ReactedAt { get; set; }
    }
}
