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

        public BotController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IUserRepository userRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
            : base(userManager, userRepository)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        // ── GET: /Bot/Chat ───────────────────────────
        public async Task<IActionResult> Chat()
        {
            var userId = _userManager.GetUserId(User)!;

            var botConv = await _context.Conversations
                .Include(c => c.Messages.OrderBy(m => m.SentAt))
                    .ThenInclude(m => m.Sender)
                .Include(c => c.Participants)
                .Where(c =>
                    c.Type == ConversationType.Support &&
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

                await _context.ConversationParticipants
                    .AddAsync(new ConversationParticipant
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
                    .Include(c => c.Messages.OrderBy(m => m.SentAt))
                        .ThenInclude(m => m.Sender)
                    .Include(c => c.Participants)
                    .FirstAsync(c => c.Id == botConv.Id);
            }

            var apiKey = _config["Gemini:ApiKey"];
            ViewBag.CurrentUserId = userId;
            ViewBag.IsBot = true;
            ViewBag.BotMode = string.IsNullOrWhiteSpace(apiKey)
                ? "fallback" : "ai";

            return View("~/Views/Bot/Chat.cshtml", botConv);
        }

        // ── POST: /Bot/SendToBot ─────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendToBot(
            int conversationId,
            string content,
            int? replyToId = null)
        {
            var apiKeyDebug = _config["Gemini:ApiKey"];
            Console.WriteLine($"[BOT DEBUG] API Key found: {!string.IsNullOrWhiteSpace(apiKeyDebug)}");
            Console.WriteLine($"[BOT DEBUG] API Key prefix: {apiKeyDebug?[..Math.Min(8, apiKeyDebug?.Length ?? 0)]}");
            Console.WriteLine($"[BOT DEBUG] Content: {content}");
            Console.WriteLine($"[BOT DEBUG] ConvId: {conversationId}");
            var userId = _userManager.GetUserId(User)!;
            var user = await _userRepository.GetByIdAsync(userId);

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { error = "empty" });

            // ── 1. Grab history BEFORE saving new message ──
            var history = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // ── 2. Build account snapshot ───────────────
            var accountContext =
                await BuildAccountContextAsync(userId, user);

            // ── 3. Save user message ────────────────────
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

            // ── 4. Call Gemini ──────────────────────────
            var botText = await CallGeminiAsync(
                content.Trim(), history, accountContext, user);

            // ── 5. Save bot message ─────────────────────
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

            var conv = await _context.Conversations
                .FindAsync(conversationId);
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

        // ── GET: /Bot/TestGemini ─────────────────────
        // Visit this URL to verify the API key works
        public async Task<IActionResult> TestGemini()
        {
            var apiKey = _config["Gemini:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                return Content(
                    "❌ NO API KEY FOUND.\n\n" +
                    "Run: dotnet user-secrets set \"Gemini:ApiKey\" \"your-key\"\n" +
                    "Or add to appsettings.json:\n" +
                    "{\n  \"Gemini\": {\n    \"ApiKey\": \"your-key\"\n  }\n}",
                    "text/plain");

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(20);

                var url =
                    "https://generativelanguage.googleapis.com/" +
                    $"v1beta/models/gemini-2.0-flash-lite" +
                    $"generateContent?key={apiKey}";

                var body = JsonSerializer.Serialize(new
                {
                    contents = new[]
                    {
                        new
                        {
                            role  = "user",
                            parts = new[]
                            {
                                new { text = "Reply with exactly: GEMINI_WORKS" }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = 20,
                        temperature = 0
                    }
                });

                var resp = await client.PostAsync(url,
                    new StringContent(
                        body, Encoding.UTF8, "application/json"));

                var raw = await resp.Content.ReadAsStringAsync();

                return Content(
                    $"HTTP Status : {resp.StatusCode}\n" +
                    $"Key prefix  : {apiKey[..Math.Min(12, apiKey.Length)]}...\n\n" +
                    $"Raw response:\n{raw}",
                    "text/plain");
            }
            catch (Exception ex)
            {
                return Content(
                    $"❌ Exception: {ex.Message}", "text/plain");
            }
        }

        // ── Build account context ────────────────────
        private async Task<string> BuildAccountContextAsync(
            string userId, User? user)
        {
            var wallets = await _context.Wallets
                .Include(w => w.Currency)
                .Where(w => w.UserId == userId)
                .ToListAsync();

            var recentTx = await _context.Transactions
                .Include(t => t.SenderWallet)
                    .ThenInclude(w => w!.Currency)
                .Include(t => t.ReceiverWallet)
                    .ThenInclude(w => w!.Currency)
                .Where(t =>
                    t.SenderWallet!.UserId == userId ||
                    t.ReceiverWallet!.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToListAsync();

            var kyc = await _context.KYCDocuments
                .Where(k => k.UserId == userId)
                .OrderByDescending(k => k.SubmittedAt)
                .FirstOrDefaultAsync();

            var pendingWallets = await _context.WalletRequests
                .Where(r =>
                    r.UserId == userId &&
                    r.Status == WalletRequestStatus.Pending)
                .CountAsync();

            var sb = new StringBuilder();
            sb.AppendLine("=== USER ACCOUNT ===");
            sb.AppendLine(
                $"Name: {user?.FirstName} {user?.LastName}");
            sb.AppendLine($"Username: @{user?.UserName}");
            sb.AppendLine($"Phone: {user?.PhoneNumber}");
            sb.AppendLine(
                $"Account type: {user?.AccountType}");
            sb.AppendLine(
                $"Verified: {(user?.IsVerified == true ? "Yes" : "No")}");
            sb.AppendLine(
                $"Member since: {user?.CreatedAt:MMMM yyyy}");
            sb.AppendLine(
                $"Total transactions done: {user?.TransactionCount}");
            sb.AppendLine();

            sb.AppendLine("=== WALLETS ===");
            if (wallets.Any())
            {
                foreach (var w in wallets)
                    sb.AppendLine(
                        $"- {w.Currency?.Code}: " +
                        $"balance {w.Balance:F2}, " +
                        $"status {(w.IsActive ? "active" : "inactive")}");
            }
            else sb.AppendLine("No wallets.");

            if (pendingWallets > 0)
                sb.AppendLine(
                    $"Pending wallet requests: {pendingWallets}");
            sb.AppendLine();

            sb.AppendLine("=== LAST 5 TRANSACTIONS ===");
            if (recentTx.Any())
            {
                foreach (var t in recentTx)
                {
                    var dir = t.SenderWallet?.UserId == userId
                        ? "SENT" : "RECEIVED";
                    sb.AppendLine(
                        $"- [{dir}] {t.Amount:F2} " +
                        $"{t.SenderWallet?.Currency?.Code} → " +
                        $"{t.ReceiverWallet?.Currency?.Code}, " +
                        $"fee {t.FeeAmount:F2}, " +
                        $"status {t.Status}, " +
                        $"date {t.CreatedAt:MMM dd yyyy}");
                }
            }
            else sb.AppendLine("No transactions yet.");
            sb.AppendLine();

            sb.AppendLine("=== KYC ===");
            if (kyc != null)
                sb.AppendLine(
                    $"Type: {kyc.DocumentType}, " +
                    $"Status: {kyc.Status}, " +
                    $"Submitted: {kyc.SubmittedAt:MMM dd yyyy}");
            else
                sb.AppendLine("No KYC submitted yet.");

            return sb.ToString();
        }


        // ── Call Gemini API ──────────────────────────
        private async Task<string> CallGeminiAsync(
     string userMessage,
     List<Message> historyBeforeThisMessage,
     string accountContext,
     User? user)
        {
            var apiKey = _config["Gemini:ApiKey"];
            var modelName = _config["Gemini:Model"] ?? "gemini-2.0-flash-lite";

            if (string.IsNullOrWhiteSpace(apiKey))
                return FallbackResponse(userMessage.ToLower(), user);

            // Simplified system prompt to reduce token usage
            var systemPrompt =
                "You are Cedar Bot, a helpful AI assistant for CedarPay, a Lebanese money transfer platform.\n" +
                "Be warm, concise, and use emojis naturally. Reply in the same language as the user.\n" +
                "Use **bold** for important terms. Keep responses under 150 words.\n\n" +
                "User account data:\n" + accountContext;

            // Simplified content building
            var contents = new List<object>();

            // Add conversation history (last 5 messages to save tokens)
            var recentHistory = historyBeforeThisMessage.TakeLast(5).ToList();
            foreach (var msg in recentHistory)
            {
                if (string.IsNullOrWhiteSpace(msg.Content)) continue;
                var role = msg.IsFromAdmin ? "model" : "user";
                contents.Add(new
                {
                    role,
                    parts = new[] { new { text = msg.Content } }
                });
            }

            // Add current user message
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                contents,
                generationConfig = new
                {
                    maxOutputTokens = 512,
                    temperature = 0.7,
                    topP = 0.95
                }
            };

            // Retry logic
            var maxRetries = 3;
            var delay = 2000; // Start with 2 seconds delay

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);

                    // Use v1 API instead of v1beta - sometimes more stable
                    var url = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent?key={apiKey}";

                    Console.WriteLine($"[GEMINI] Attempt {attempt} - Model: {modelName}");
                    Console.WriteLine($"[GEMINI] URL: {url.Replace(apiKey, "HIDDEN")}");

                    var json = JsonSerializer.Serialize(requestBody);
                    var payload = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, payload);
                    var raw = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"[GEMINI] HTTP Status: {(int)response.StatusCode}");

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"[GEMINI] Rate limited. Waiting {delay}ms...");
                        await Task.Delay(delay);
                        delay *= 2;
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[GEMINI] Error response: {raw}");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(delay);
                            delay *= 2;
                            continue;
                        }
                        return FallbackResponse(userMessage.ToLower(), user);
                    }

                    var doc = JsonDocument.Parse(raw);

                    // Check if we have valid response
                    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                        candidates.GetArrayLength() == 0)
                    {
                        Console.WriteLine("[GEMINI] No candidates in response");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(delay);
                            delay *= 2;
                            continue;
                        }
                        return FallbackResponse(userMessage.ToLower(), user);
                    }

                    var first = candidates[0];

                    // Check finish reason
                    if (first.TryGetProperty("finishReason", out var finishReason))
                    {
                        var reason = finishReason.GetString();
                        if (reason == "SAFETY")
                        {
                            Console.WriteLine("[GEMINI] Response blocked by safety filters");
                            return FallbackResponse(userMessage.ToLower(), user);
                        }
                    }

                    // Extract the text
                    var text = first
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Console.WriteLine($"[GEMINI] Success! Response: {text[..Math.Min(100, text.Length)]}...");
                        return text;
                    }

                    return FallbackResponse(userMessage.ToLower(), user);
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine($"[GEMINI] Timeout on attempt {attempt}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                    else
                    {
                        return "⏱️ Sorry, I'm having trouble responding right now. Please try again in a moment!";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GEMINI] Exception on attempt {attempt}: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                    else
                    {
                        return FallbackResponse(userMessage.ToLower(), user);
                    }
                }
            }

            return FallbackResponse(userMessage.ToLower(), user);
        }

        private string? GetTextFromContent(object content)
        {
            try
            {
                var json = JsonSerializer.Serialize(content);
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
                return text;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet]
        public async Task<IActionResult> ListModels()
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return Content("Gemini API Key not found");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                var doc = JsonDocument.Parse(content);
                var models = doc.RootElement.GetProperty("models");

                var sb = new StringBuilder();
                sb.AppendLine("Available models:\n");
                foreach (var model in models.EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    sb.AppendLine($"- {name}");
                }

                return Content(sb.ToString());
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }

        // ── Fallback responses ───────────────────────
        private static string FallbackResponse(
            string input, User? user)
        {
            var name = user?.FirstName ?? "there";

            if (input.Contains("hello") ||
                input.Contains("hi") ||
                input.Contains("hey") ||
                input.Contains("مرحبا") ||
                input.Contains("bonjour"))
                return $"Hello {name}! 👋 How can I help you " +
                       "with CedarPay today?";

            if (input.Contains("balance") ||
                input.Contains("wallet") ||
                input.Contains("رصيد"))
                return "💰 Go to **Wallets** in the sidebar to " +
                       "see your balances and active wallets.";

            if (input.Contains("transfer") ||
                input.Contains("send") ||
                input.Contains("تحويل"))
                return "💸 Go to **Send Money** in the sidebar. " +
                       "Fee is 2% + $0.50. Every 10th transfer " +
                       "is FREE!";

            if (input.Contains("fee") ||
                input.Contains("رسوم"))
                return "💰 Transfer fee: **2% + $0.50** per " +
                       "transaction. Every 10th is FREE!";

            if (input.Contains("exchange") ||
                input.Contains("rate") ||
                input.Contains("currency") ||
                input.Contains("صرف"))
                return "💱 We support USD, EUR, LBP, AED, GBP, " +
                       "SAR, TRY, EGP, JOD, KWD. Rates update " +
                       "hourly from live data.";

            if (input.Contains("kyc") ||
                input.Contains("verify") ||
                input.Contains("توثيق"))
                return "🛡️ Go to **Profile** to complete KYC. " +
                       "Upload your ID documents to get verified.";

            if (input.Contains("agent") ||
                input.Contains("cash"))
                return "🏪 Use the **Agent Map** to find a " +
                       "CedarPay agent near you for cash in/out.";

            if (input.Contains("top") ||
                input.Contains("recharge") ||
                input.Contains("stripe"))
                return "💳 Go to **Top Up** to add funds via " +
                       "credit card (Stripe) or bank transfer.";

            if (input.Contains("help") ||
                input.Contains("مساعدة"))
                return "I can help with:\n" +
                       "• 💰 Balances & wallets\n" +
                       "• 💸 Sending money\n" +
                       "• 💱 Exchange rates\n" +
                       "• 🛡️ KYC verification\n" +
                       "• 🏪 Finding agents\n\n" +
                       "What do you need?";

            return $"I'm not sure I understand, {name}. " +
                   "Try asking about your balance, transfers, " +
                   "fees, or exchange rates. Type **help** for " +
                   "options! 😊";
        }

        [HttpGet]
        public async Task<IActionResult> TestModels()
        {
            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return Content("Gemini API Key not found");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return Content($"Error: {response.StatusCode}\n{content}");

                var doc = JsonDocument.Parse(content);
                var models = doc.RootElement.GetProperty("models");

                var sb = new StringBuilder();
                sb.AppendLine("=== AVAILABLE MODELS ===\n");

                foreach (var model in models.EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    var displayName = model.GetProperty("displayName").GetString();
                    var supportedMethods = model.GetProperty("supportedGenerationMethods");

                    // Check if it supports generateContent
                    var supportsGenerate = false;
                    foreach (var method in supportedMethods.EnumerateArray())
                    {
                        if (method.GetString() == "generateContent")
                        {
                            supportsGenerate = true;
                            break;
                        }
                    }

                    if (supportsGenerate)
                    {
                        sb.AppendLine($"✅ {name} - {displayName}");
                    }
                }

                return Content(sb.ToString());
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
    }
}