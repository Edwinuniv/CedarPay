using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using System.Text;
using System.Text.Json;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class BotController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public BotController(ApplicationDbContext context, UserManager<User> userManager, IUserRepository userRepository, IHttpClientFactory httpClientFactory, IConfiguration config): base(userManager, userRepository)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<IActionResult> Chat()
        {
            var userId = _userManager.GetUserId(User)!;

            var botConv = await _context.Conversations
                .Include(c => c.Messages.OrderBy(m => m.SentAt)).ThenInclude(m => m.Sender)
                .Include(c => c.Participants)
                .Where(c => c.Type == ConversationType.Support &&
                            c.Participants.Any(p => p.UserId == userId) &&
                            c.Participants.Count() == 1)
                .FirstOrDefaultAsync();

            if (botConv == null)
            {
                botConv = new Conversation
                {
                    UserId = userId,
                    Title = "CedarPay Assistant",
                    Type = ConversationType.Support,
                    CreatedAt = DateTime.Now,
                    LastMessageAt = DateTime.Now
                };
                await _context.Conversations.AddAsync(botConv);
                await _context.SaveChangesAsync();

                await _context.ConversationParticipants.AddAsync(new ConversationParticipant
                {
                    ConversationId = botConv.Id,
                    UserId = userId,
                    JoinedAt = DateTime.Now
                });

                await _context.Messages.AddAsync(new Message
                {
                    Content =
                        "👋 Hi! I'm **Cedar Bot**, your CedarPay assistant.\n\n" +
                        "I have live access to your account and can help with:\n" +
                        "• 💰 Check your wallet balances\n" +
                        "• 📊 Review your recent transactions\n" +
                        "• 💸 Explain transfer fees & how to send money\n" +
                        "• 🔄 Currency exchange rates & supported currencies\n" +
                        "• 🛡️ KYC verification guidance\n" +
                        "• 🏪 How to find agents for cash in/out\n" +
                        "• ❓ Any other CedarPay question\n\n" +
                        "What can I help you with today?",
                    SenderId = userId,
                    ConversationId = botConv.Id,
                    SentAt = DateTime.Now,
                    IsFromAdmin = true,
                    IsRead = true
                });

                await _context.SaveChangesAsync();

                botConv = await _context.Conversations
                    .Include(c => c.Messages.OrderBy(m => m.SentAt)).ThenInclude(m => m.Sender)
                    .Include(c => c.Participants)
                    .FirstAsync(c => c.Id == botConv.Id);
            }

            var apiKey = _config["Gemini:ApiKey"];
            ViewBag.CurrentUserId = userId;
            ViewBag.IsBot = true;
            ViewBag.BotMode = string.IsNullOrWhiteSpace(apiKey) ? "fallback" : "ai";

            return View("~/Views/Bot/Chat.cshtml", botConv);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToBot(int conversationId, string content, int? replyToId = null)
        {
            var userId = _userManager.GetUserId(User)!;
            var user = await _userRepository.GetByIdAsync(userId);

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { error = "empty" });

            var history = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var accountContext = await BuildAccountContextAsync(userId, user);

            var userMsg = new Message
            {
                Content = content.Trim(),
                SenderId = userId,
                ConversationId = conversationId,
                SentAt = DateTime.Now,
                IsRead = true,
                IsFromAdmin = false,
                ReplyToId = replyToId
            };
            await _context.Messages.AddAsync(userMsg);
            await _context.SaveChangesAsync();

            var botText = await CallGeminiAsync(content.Trim(), history, accountContext, user);

            var botMsg = new Message
            {
                Content = botText,
                SenderId = userId,
                ConversationId = conversationId,
                SentAt = DateTime.Now.AddMilliseconds(300),
                IsFromAdmin = true,
                IsRead = true
            };
            await _context.Messages.AddAsync(botMsg);

            var conv = await _context.Conversations.FindAsync(conversationId);
            if (conv != null)
            {
                conv.LastMessageAt = DateTime.Now;
                _context.Conversations.Update(conv);
            }
            await _context.SaveChangesAsync();

            return Json(new
            {
                userMessageId = userMsg.Id,
                botMessageId = botMsg.Id,
                botResponse = botText,
                sentAt = DateTime.Now.ToString("HH:mm")
            });
        }

        private async Task<string> BuildAccountContextAsync(string userId, User? user)
        {
            var wallets = await _context.Wallets.Include(w => w.Currency).Where(w => w.UserId == userId).ToListAsync();

            var recentTx = await _context.Transactions
                .Include(t => t.SenderWallet).ThenInclude(w => w!.Currency)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w!.Currency)
                .Where(t => t.SenderWallet!.UserId == userId || t.ReceiverWallet!.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            var kyc = await _context.KYCDocuments
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.SubmittedAt)
                .FirstOrDefaultAsync();

            var pendingWallets = await _context.WalletRequests
                .Where(r => r.UserId == userId && r.Status == WalletRequestStatus.Pending)
                .CountAsync();

            var sb = new StringBuilder();
            sb.AppendLine("USER ACCOUNT");
            sb.AppendLine($"Name: {user?.FirstName} {user?.LastName}");
            sb.AppendLine($"Username: @{user?.UserName}");
            sb.AppendLine($"Phone: {user?.PhoneNumber}");
            sb.AppendLine($"Account type: {user?.AccountType}");
            sb.AppendLine($"Verified: {(user?.IsVerified == true ? "Yes" : "No")}");
            sb.AppendLine($"Member since: {user?.CreatedAt:MMMM yyyy}");
            sb.AppendLine($"Total transactions done: {user?.TransactionCount}");
            sb.AppendLine();

            sb.AppendLine("WALLETS");
            if (wallets.Any())
                foreach (var w in wallets)
                    sb.AppendLine($"- {w.Currency?.Code}: balance {w.Balance:F2}, status {(w.IsActive ? "active" : "inactive")}");
            else
                sb.AppendLine("No wallets.");

            if (pendingWallets > 0) sb.AppendLine($"Pending wallet requests: {pendingWallets}");
            sb.AppendLine();

            sb.AppendLine("LAST 5 TRANSACTIONS");
            if (recentTx.Any())
                foreach (var t in recentTx)
                {
                    var dir = t.SenderWallet?.UserId == userId ? "SENT" : "RECEIVED";
                    sb.AppendLine($"- [{dir}] {t.Amount:F2} {t.SenderWallet?.Currency?.Code} → {t.ReceiverWallet?.Currency?.Code}, fee {t.FeeAmount:F2}, status {t.Status}, date {t.CreatedAt:MMM dd yyyy}");
                }
            else
                sb.AppendLine("No transactions yet.");
            sb.AppendLine();

            sb.AppendLine("KYC");
            if (kyc != null)
                sb.AppendLine($"Type: {kyc.DocumentType}, Status: {kyc.Status}, Submitted: {kyc.SubmittedAt:MMM dd yyyy}");
            else
                sb.AppendLine("No KYC submitted yet.");

            return sb.ToString();
        }

        private async Task<string> CallGeminiAsync(string userMessage, List<Message> historyBeforeThisMessage, string accountContext, User? user)
        {
            var apiKey = _config["Gemini:ApiKey"];
            var modelName = _config["Gemini:Model"] ?? "gemini-2.0-flash-lite";

            if (string.IsNullOrWhiteSpace(apiKey))
                return FallbackResponse(userMessage.ToLower(), user);

            var systemPrompt =
                "You are Cedar Bot, a helpful AI assistant for CedarPay, a Lebanese money transfer platform.\n" +
                "Be warm, concise, and use emojis naturally. Reply in the same language as the user.\n" +
                "Use **bold** for important terms. Keep responses under 150 words.\n\n" +
                "User account data:\n" + accountContext;

            var contents = new List<object>();
            var recentHistory = historyBeforeThisMessage.TakeLast(5).ToList();

            foreach (var msg in recentHistory)
            {
                if (string.IsNullOrWhiteSpace(msg.Content)) continue;
                var role = msg.IsFromAdmin ? "model" : "user";
                contents.Add(new { role, parts = new[] { new { text = msg.Content } } });
            }

            contents.Add(new { role = "user", parts = new[] { new { text = userMessage } } });

            var requestBody = new
            {
                contents,
                generationConfig = new { maxOutputTokens = 512, temperature = 0.7, topP = 0.95 }
            };

            var maxRetries = 3;
            var delay = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);

                    var url = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent?key={apiKey}";
                    var json = JsonSerializer.Serialize(requestBody);
                    var payload = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, payload);
                    var raw = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        await Task.Delay(delay);
                        delay *= 2;
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if (attempt < maxRetries) { await Task.Delay(delay); delay *= 2; continue; }
                        return FallbackResponse(userMessage.ToLower(), user);
                    }

                    var doc = JsonDocument.Parse(raw);

                    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    {
                        if (attempt < maxRetries) { await Task.Delay(delay); delay *= 2; continue; }
                        return FallbackResponse(userMessage.ToLower(), user);
                    }

                    var first = candidates[0];

                    if (first.TryGetProperty("finishReason", out var finishReason) && finishReason.GetString() == "SAFETY")
                        return FallbackResponse(userMessage.ToLower(), user);

                    var text = first.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                    if (!string.IsNullOrWhiteSpace(text)) return text;

                    return FallbackResponse(userMessage.ToLower(), user);
                }
                catch (TaskCanceledException)
                {
                    if (attempt < maxRetries) { await Task.Delay(delay); delay *= 2; }
                    else return "⏱️ Sorry, I'm having trouble responding right now. Please try again in a moment!";
                }
                catch (Exception)
                {
                    if (attempt < maxRetries) { await Task.Delay(delay); delay *= 2; }
                    else return FallbackResponse(userMessage.ToLower(), user);
                }
            }

            return FallbackResponse(userMessage.ToLower(), user);
        }

        private static string FallbackResponse(string input, User? user)
        {
            var name = user?.FirstName ?? "there";

            if (input.Contains("hello") || input.Contains("hi") || input.Contains("hey") || input.Contains("مرحبا") || input.Contains("bonjour"))
                return $"Hello {name}! 👋 How can I help you with CedarPay today?";

            if (input.Contains("balance") || input.Contains("wallet") || input.Contains("رصيد"))
                return "💰 Go to **Wallets** in the sidebar to see your balances and active wallets.";

            if (input.Contains("transfer") || input.Contains("send") || input.Contains("تحويل"))
                return "💸 Go to **Send Money** in the sidebar. Fee is 2% + $0.50. Every 10th transfer is FREE!";

            if (input.Contains("fee") || input.Contains("رسوم"))
                return "💰 Transfer fee: **2% + $0.50** per transaction. Every 10th is FREE!";

            if (input.Contains("exchange") || input.Contains("rate") || input.Contains("currency") || input.Contains("صرف"))
                return "💱 We support USD, EUR, LBP, AED, GBP, SAR, TRY, EGP, JOD, KWD. Rates update hourly from live data.";

            if (input.Contains("kyc") || input.Contains("verify") || input.Contains("توثيق"))
                return "🛡️ Go to **Profile** to complete KYC. Upload your ID documents to get verified.";

            if (input.Contains("agent") || input.Contains("cash"))
                return "🏪 Use the **Agent Map** to find a CedarPay agent near you for cash in/out.";

            if (input.Contains("top") || input.Contains("recharge") || input.Contains("stripe"))
                return "💳 Go to **Top Up** to add funds via credit card (Stripe) or bank transfer.";

            if (input.Contains("help") || input.Contains("مساعدة"))
                return "I can help with:\n• 💰 Balances & wallets\n• 💸 Sending money\n• 💱 Exchange rates\n• 🛡️ KYC verification\n• 🏪 Finding agents\n\nWhat do you need?";

            return $"I'm not sure I understand, {name}. Try asking about your balance, transfers, fees, or exchange rates. Type **help** for options! 😊";
        }
    }
}