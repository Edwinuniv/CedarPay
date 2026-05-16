using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MoneyTransfer.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        public async Task SendMessage(int conversationId, string senderId, string senderName, string content, string? senderPicture)
        {
            await Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", 
                new
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    SenderName = senderName,
                    SenderPicture = senderPicture,
                    Content = content,
                    SentAt = DateTime.Now
                        .ToString("HH:mm"),
                    IsOwn = false
                });
        }

        public async Task JoinConversation(int conversationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
        }

        public async Task EditMessage(int conversationId, int messageId, string newContent)
        {
            await Clients.Group($"conv_{conversationId}").SendAsync("MessageEdited",
                new
                {
                    MessageId = messageId,
                    NewContent = newContent
                });
        }

        public async Task DeleteMessage(int conversationId, int messageId, bool deletedForEveryone)
        {
            await Clients.Group($"conv_{conversationId}").SendAsync("MessageDeleted",
                new
                {
                    MessageId = messageId,
                    DeletedForEveryone = deletedForEveryone
                });
        }

        public async Task BroadcastReaction(int conversationId, int messageId, string emoji, bool added, string userId, object counts)
        {
            await Clients.Group($"conv_{conversationId}")
                .SendAsync("ReactionUpdated", new
                {
                    MessageId = messageId,
                    Emoji = emoji,
                    Added = added,
                    UserId = userId,
                    Counts = counts
                });
        }
    }
}