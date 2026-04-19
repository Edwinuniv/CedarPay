using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public ChatController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IUserRepository userRepository)
            : base(userManager, userRepository)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            // Get existing conversations
            var convs = await _context.Conversations
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages
                    .OrderByDescending(m => m.SentAt).Take(1))
                    .ThenInclude(m => m.Sender)
                .Where(c => c.Participants
                    .Any(p => p.UserId == userId))
                .OrderByDescending(c =>
                    c.LastMessageAt ?? c.CreatedAt)
                .ToListAsync();

            var beneficiaries = await _context.Beneficiaries
                .Include(b => b.ReceiverUser)
                .Where(b => b.UserId == userId &&
                            b.ReceiverUserId != null)
                .ToListAsync();

            var convParticipantIds = convs
                .SelectMany(c => c.Participants)
                .Where(p => p.UserId != userId)
                .Select(p => p.UserId)
                .Distinct()
                .ToList();

            var beneficiariesNotInChat = beneficiaries
                .Where(b => b.ReceiverUserId != null &&
                            !convParticipantIds.Contains(
                                b.ReceiverUserId!))
                .ToList();

            ViewBag.CurrentUserId = userId;
            ViewBag.BeneficiariesNotInChat =
                beneficiariesNotInChat;

            return View(convs);
        }

        public async Task<IActionResult> Open(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var conv = await _context.Conversations
                .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
                .Include(c => c.Messages
                    .OrderBy(m => m.SentAt))
                    .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c => c.Id == id &&
                    c.Participants.Any(p => p.UserId == userId));

            if (conv == null) return NotFound();

            var unread = conv.Messages
                .Where(m => !m.IsRead && m.SenderId != userId)
                .ToList();
            foreach (var msg in unread) msg.IsRead = true;
            if (unread.Any())
                await _context.SaveChangesAsync();

            ViewBag.CurrentUserId = userId;
            return View(conv);
        }

        public IActionResult New()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindAndStart(
            string phoneNumber)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                ModelState.AddModelError("",
                    "Please enter a phone number.");
                return View("New");
            }

            var recipient = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.PhoneNumber == phoneNumber &&
                    u.Id != userId);

            if (recipient == null)
            {
                ViewBag.Error =
                    "No user found with that phone number. " +
                    "Make sure they are registered on CedarPay.";
                return View("New");
            }

            var existing = await _context.Conversations
                .Include(c => c.Participants)
                .Where(c =>
                    c.Participants.Any(p => p.UserId == userId) &&
                    c.Participants.Any(p =>
                        p.UserId == recipient.Id) &&
                    c.Participants.Count() == 2)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                return RedirectToAction("Open",
                    new { id = existing.Id });
            }

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
                new ConversationParticipant
                {
                    ConversationId = conv.Id,
                    UserId = userId,
                    JoinedAt = DateTime.Now
                },
                new ConversationParticipant
                {
                    ConversationId = conv.Id,
                    UserId = recipient.Id,
                    JoinedAt = DateTime.Now
                });

            await _context.SaveChangesAsync();

            return RedirectToAction("Open",
                new { id = conv.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(
            int conversationId, string content)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(content))
                return RedirectToAction("Open",
                    new { id = conversationId });

            var isParticipant = await _context.ConversationParticipants.AnyAsync(
                p => p.ConversationId == conversationId && p.UserId == userId);

            if (!isParticipant)
            {
                return Unauthorized();
            }
            await _context.Messages.AddAsync(new Message
            {
                Content = content.Trim(),
                SenderId = userId,
                ConversationId = conversationId,
                SentAt = DateTime.Now,
                IsRead = false,
                IsFromAdmin = User.IsInRole("admin")
            });

            var conv = await _context.Conversations
                .FindAsync(conversationId);
            if (conv != null)
            {
                conv.LastMessageAt = DateTime.Now;
                _context.Conversations.Update(conv);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Open", new { id = conversationId });
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = _userManager.GetUserId(User)!;
            var count = await _context.Messages.Where(m => !m.IsRead && m.SenderId != userId && _context.ConversationParticipants
                             .Any(p => p.ConversationId == m.ConversationId && p.UserId == userId)).CountAsync();
            return Json(new { count });
        }
    }
}