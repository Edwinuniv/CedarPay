using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Hubs;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IFileUploadService _fileUploadService;

        public ChatController(ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository, IHubContext<ChatHub> hubContext, IEmailService emailService, IFileUploadService fileUploadService): base(userManager, userRepository)
        {
            _context = context;
            _hubContext = hubContext;
            _emailService = emailService;
            _fileUploadService = fileUploadService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var convs = await _context.Conversations
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1)).ThenInclude(m => m.Sender)
                .Where(c => c.Participants.Any(p => p.UserId == userId) && c.Type != ConversationType.Support)
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .ToListAsync();

            var beneficiaries = await _context.Beneficiaries
                .Include(b => b.ReceiverUser)
                .Where(b => b.UserId == userId && b.ReceiverUserId != null)
                .ToListAsync();

            var convParticipantIds = convs
                .SelectMany(c => c.Participants)
                .Where(p => p.UserId != userId)
                .Select(p => p.UserId)
                .Distinct()
                .ToList();

            var beneficiariesNotInChat = beneficiaries
                .Where(b => b.ReceiverUserId != null && !convParticipantIds.Contains(b.ReceiverUserId!))
                .ToList();

            ViewBag.CurrentUserId = userId;
            ViewBag.BeneficiariesNotInChat = beneficiariesNotInChat;

            return View(convs);
        }

        public async Task<IActionResult> Open(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var conv = await _context.Conversations
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages.OrderBy(m => m.SentAt)).ThenInclude(m => m.Sender)
                .Include(c => c.Messages).ThenInclude(m => m.ReplyTo).ThenInclude(r => r!.Sender)
                .Include(c => c.Messages).ThenInclude(m => m.Reactions)
                .FirstOrDefaultAsync(c => c.Id == id && c.Participants.Any(p => p.UserId == userId));

            if (conv == null) return NotFound();

            var unread = conv.Messages.Where(m => !m.IsRead && m.SenderId != userId).ToList();
            foreach (var msg in unread) msg.IsRead = true;
            if (unread.Any()) await _context.SaveChangesAsync();

            ViewBag.CurrentUserId = userId;
            return View(conv);
        }

        public IActionResult New() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindAndStart(string phoneNumber)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                ModelState.AddModelError("", "Please enter a phone number.");
                ViewBag.ActiveTab = "phone";
                return View("New");
            }

            var normalized = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            var recipient = await _context.Users
                .ToListAsync()
                .ContinueWith(t => t.Result.FirstOrDefault(u =>
                    u.Id != userId &&
                    (u.PhoneNumber ?? "").Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == normalized));

            if (recipient == null)
            {
                ViewBag.Error = "No user found with that phone number. Make sure they are registered on CedarPay.";
                ViewBag.ActiveTab = "phone";
                return View("New");
            }

            if (recipient.Id == userId)
            {
                ViewBag.Error = "You cannot start a chat with yourself.";
                ViewBag.ActiveTab = "phone";
                return View("New");
            }

            var existing = await _context.Conversations
                .Include(c => c.Participants)
                .Where(c => c.Participants.Any(p => p.UserId == userId) &&
                            c.Participants.Any(p => p.UserId == recipient.Id) &&
                            c.Participants.Count() == 2)
                .FirstOrDefaultAsync();

            if (existing != null) return RedirectToAction("Open", new { id = existing.Id });

            var conv = new Conversation
            {
                UserId = userId,
                Title = null,
                Type = ConversationType.UserToUser,
                CreatedAt = DateTime.Now,
                LastMessageAt = DateTime.Now
            };

            await _context.Conversations.AddAsync(conv);
            await _context.SaveChangesAsync();

            await _context.ConversationParticipants.AddRangeAsync(
                new ConversationParticipant { ConversationId = conv.Id, UserId = userId, JoinedAt = DateTime.Now },
                new ConversationParticipant { ConversationId = conv.Id, UserId = recipient.Id, JoinedAt = DateTime.Now });

            await _context.SaveChangesAsync();
            return RedirectToAction("Open", new { id = conv.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindByUsername(string username)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(username))
            {
                ViewBag.Error = "Please enter a username.";
                ViewBag.ActiveTab = "username";
                return View("New");
            }

            var clean = username.TrimStart('@').Trim();
            var recipient = await _context.Users.FirstOrDefaultAsync(u => u.UserName == clean && u.Id != userId);

            if (recipient == null)
            {
                ViewBag.Error = $"No user found with username \"@{clean}\". Make sure they are registered on CedarPay.";
                ViewBag.ActiveTab = "username";
                return View("New");
            }

            if (recipient.Id == userId)
            {
                ViewBag.Error = "You cannot start a chat with yourself.";
                ViewBag.ActiveTab = "username";
                return View("New");
            }

            var existing = await _context.Conversations
                .Include(c => c.Participants)
                .Where(c => c.Participants.Any(p => p.UserId == userId) &&
                            c.Participants.Any(p => p.UserId == recipient.Id) &&
                            c.Participants.Count() == 2)
                .FirstOrDefaultAsync();

            if (existing != null) return RedirectToAction("Open", new { id = existing.Id });

            var conv = new Conversation
            {
                UserId = userId,
                Title = null,
                Type = ConversationType.UserToUser,
                CreatedAt = DateTime.Now,
                LastMessageAt = DateTime.Now
            };

            await _context.Conversations.AddAsync(conv);
            await _context.SaveChangesAsync();

            await _context.ConversationParticipants.AddRangeAsync(
                new ConversationParticipant { ConversationId = conv.Id, UserId = userId, JoinedAt = DateTime.Now },
                new ConversationParticipant { ConversationId = conv.Id, UserId = recipient.Id, JoinedAt = DateTime.Now });

            await _context.SaveChangesAsync();
            return RedirectToAction("Open", new { id = conv.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(int conversationId, string content, int? replyToId)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(content))
                return RedirectToAction("Open", new { id = conversationId });

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

            if (!isParticipant) return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);

            var msg = new Message
            {
                Content = content.Trim(),
                SenderId = userId,
                ConversationId = conversationId,
                SentAt = DateTime.Now,
                IsRead = false,
                IsFromAdmin = User.IsInRole("admin"),
                ReplyToId = replyToId
            };

            await _context.Messages.AddAsync(msg);

            var conv = await _context.Conversations.FindAsync(conversationId);
            if (conv != null)
            {
                conv.LastMessageAt = DateTime.Now;
                _context.Conversations.Update(conv);
            }

            await _context.SaveChangesAsync();

            string? replyPreview = null;
            string? replySender = null;
            if (replyToId.HasValue)
            {
                var replied = await _context.Messages.Include(m => m.Sender).FirstOrDefaultAsync(m => m.Id == replyToId);
                replyPreview = replied?.Content;
                replySender = $"{replied?.Sender?.FirstName} {replied?.Sender?.LastName}";
            }

            await _hubContext.Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", new
            {
                ConversationId = conversationId,
                SenderId = userId,
                SenderName = $"{user?.FirstName} {user?.LastName}",
                SenderPicture = user?.ProfilePictureUrl,
                Content = content.Trim(),
                SentAt = msg.SentAt.ToString("HH:mm"),
                IsOwn = false,
                MessageId = msg.Id,
                MessageType = "Text",
                MediaUrl = (string?)null,
                MediaFileName = (string?)null,
                ReplyPreview = replyPreview,
                ReplySender = replySender
            });

            var participants = await _context.ConversationParticipants
                .Include(p => p.User)
                .Where(p => p.ConversationId == conversationId && p.UserId != userId)
                .ToListAsync();

            foreach (var participant in participants)
            {
                if (participant.User?.Email != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await _emailService.SendChatMessageAsync(
                            participant.User.Email,
                            $"{participant.User.FirstName} {participant.User.LastName}",
                            $"{user?.FirstName} {user?.LastName}",
                            content.Trim());
                    });
                }
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, messageId = msg.Id, sentAt = msg.SentAt.ToString("HH:mm") });

            return RedirectToAction("Open", new { id = conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(104_857_600)]
        public async Task<IActionResult> SendFile(int conversationId, IFormFile file, int? replyToId)
        {
            var userId = _userManager.GetUserId(User)!;

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

            if (!isParticipant) return Unauthorized();

            if (file == null || file.Length == 0)
                return RedirectToAction("Open", new { id = conversationId });

            if (!_fileUploadService.IsAllowedFile(file))
            {
                TempData["ChatError"] = "File type not allowed.";
                return RedirectToAction("Open", new { id = conversationId });
            }

            if (file.Length > 104_857_600)
            {
                TempData["ChatError"] = "File too large. Max 100MB.";
                return RedirectToAction("Open", new { id = conversationId });
            }

            var user = await _userRepository.GetByIdAsync(userId);
            var msgType = _fileUploadService.GetMessageType(file);
            var (url, originalName, fileSize) = await _fileUploadService.SaveChatFileAsync(file);

            var msg = new Message
            {
                Content = originalName,
                SenderId = userId,
                ConversationId = conversationId,
                SentAt = DateTime.Now,
                IsRead = false,
                IsFromAdmin = User.IsInRole("admin"),
                ReplyToId = replyToId,
                MessageType = msgType,
                MediaUrl = url,
                MediaFileName = originalName,
                MediaFileSize = fileSize
            };

            await _context.Messages.AddAsync(msg);

            var conv = await _context.Conversations.FindAsync(conversationId);
            if (conv != null)
            {
                conv.LastMessageAt = DateTime.Now;
                _context.Conversations.Update(conv);
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"conv_{conversationId}").SendAsync("ReceiveMessage", new
            {
                ConversationId = conversationId,
                SenderId = userId,
                SenderName = $"{user?.FirstName} {user?.LastName}",
                SenderPicture = user?.ProfilePictureUrl,
                Content = originalName,
                SentAt = msg.SentAt.ToString("HH:mm"),
                IsOwn = false,
                MessageId = msg.Id,
                MessageType = msgType.ToString(),
                MediaUrl = url,
                MediaFileName = originalName,
                MediaFileSize = fileSize,
                ReplyPreview = (string?)null,
                ReplySender = (string?)null
            });

            return RedirectToAction("Open", new { id = conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMessage(int messageId, string newContent)
        {
            var userId = _userManager.GetUserId(User)!;
            var msg = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userId);

            if (msg == null) return Unauthorized();
            if (msg.IsDeletedForEveryone) return BadRequest();
            if (string.IsNullOrWhiteSpace(newContent)) return BadRequest();

            msg.Content = newContent.Trim();
            msg.IsEdited = true;
            msg.EditedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"conv_{msg.ConversationId}").SendAsync("MessageEdited", new
            {
                MessageId = msg.Id,
                NewContent = msg.Content
            });

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMessage(int messageId, string deleteType)
        {
            var userId = _userManager.GetUserId(User)!;
            var msg = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);

            if (msg == null) return NotFound();

            bool isOwner = msg.SenderId == userId;

            if (deleteType == "everyone")
            {
                if (!isOwner) return Unauthorized();
                msg.IsDeletedForEveryone = true;
                msg.Content = "🚫 This message was deleted.";
            }
            else
            {
                var isParticipant = await _context.ConversationParticipants
                    .AnyAsync(p => p.ConversationId == msg.ConversationId && p.UserId == userId);

                if (!isParticipant) return Unauthorized();
                msg.IsDeletedForSender = true;
            }

            await _context.SaveChangesAsync();

            if (deleteType == "everyone")
            {
                await _hubContext.Clients.Group($"conv_{msg.ConversationId}").SendAsync("MessageDeleted", new
                {
                    MessageId = msg.Id,
                    DeletedForEveryone = true
                });
            }

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetMessage(int messageId)
        {
            var userId = _userManager.GetUserId(User)!;
            var msg = await _context.Messages.Include(m => m.Sender).FirstOrDefaultAsync(m => m.Id == messageId);

            if (msg == null) return NotFound();

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == msg.ConversationId && p.UserId == userId);

            if (!isParticipant) return Unauthorized();

            return Json(new
            {
                id = msg.Id,
                content = msg.Content,
                senderName = $"{msg.Sender?.FirstName} {msg.Sender?.LastName}",
                sentAt = msg.SentAt.ToString("HH:mm")
            });
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = _userManager.GetUserId(User)!;
            var count = await _context.Messages
                .Where(m => !m.IsRead && m.SenderId != userId &&
                    _context.ConversationParticipants.Any(p => p.ConversationId == m.ConversationId && p.UserId == userId))
                .CountAsync();
            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> ConversationProfile(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var conv = await _context.Conversations
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id && c.Participants.Any(p => p.UserId == userId));

            if (conv == null) return NotFound();

            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
            if (otherParticipant?.User == null) return NotFound();

            var otherUser = otherParticipant.User;
            var messages = conv.Messages.Where(m => !m.IsDeletedForEveryone).OrderByDescending(m => m.SentAt).ToList();

            var images = messages.Where(m => m.MessageType == MessageType.Image && m.MediaUrl != null)
                .Select(m => new { m.MediaUrl, m.SentAt }).ToList();
            var files = messages.Where(m => m.MessageType == MessageType.File && m.MediaUrl != null)
                .Select(m => new { m.MediaUrl, m.MediaFileName, m.MediaFileSize, m.SentAt }).ToList();
            var videos = messages.Where(m => m.MessageType == MessageType.Video && m.MediaUrl != null)
                .Select(m => new { m.MediaUrl, m.SentAt }).ToList();

            return Json(new
            {
                firstName = otherUser.FirstName,
                lastName = otherUser.LastName,
                username = otherUser.UserName,
                phone = otherUser.PhoneNumber,
                bio = otherUser.Description,
                profilePicture = otherUser.ProfilePictureUrl,
                memberSince = otherUser.CreatedAt.ToString("MMMM yyyy"),
                isVerified = otherUser.IsVerified,
                city = otherUser.City,
                country = otherUser.Country,
                images,
                files,
                videos
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactToMessage(int messageId, string emoji)
        {
            var userId = _userManager.GetUserId(User)!;
            var allowed = new[] { "👍", "❤️", "😂", "😮", "😢", "🙏" };

            if (!allowed.Contains(emoji)) return BadRequest();

            var msg = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (msg == null) return NotFound();

            var isParticipant = await _context.ConversationParticipants
                .AnyAsync(p => p.ConversationId == msg.ConversationId && p.UserId == userId);
            if (!isParticipant) return Unauthorized();

            var existing = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

            bool added;
            if (existing != null)
            {
                _context.MessageReactions.Remove(existing);
                added = false;
            }
            else
            {
                await _context.MessageReactions.AddAsync(new MessageReaction
                {
                    MessageId = messageId,
                    UserId = userId,
                    Emoji = emoji,
                    ReactedAt = DateTime.Now
                });
                added = true;
            }

            await _context.SaveChangesAsync();

            var counts = await _context.MessageReactions
                .Where(r => r.MessageId == messageId)
                .GroupBy(r => r.Emoji)
                .Select(g => new { Emoji = g.Key, Count = g.Count() })
                .ToListAsync();

            await _hubContext.Clients.Group($"conv_{msg.ConversationId}").SendAsync("ReactionUpdated", new
            {
                MessageId = messageId,
                Emoji = emoji,
                Added = added,
                UserId = userId,
                Counts = counts
            });

            return Ok(new { counts });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConversation(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var conv = await _context.Conversations
                .Include(c => c.Participants)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id && c.Participants.Any(p => p.UserId == userId));

            if (conv == null) return NotFound();

            _context.Messages.RemoveRange(conv.Messages);
            _context.ConversationParticipants.RemoveRange(conv.Participants);
            _context.Conversations.Remove(conv);

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}