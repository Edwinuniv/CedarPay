using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;
using MoneyTransfer.Services.Interfaces;
using MoneyTransfer.Data;
using Microsoft.EntityFrameworkCore;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class ReviewController : BaseController
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _sp;

        public ReviewController(IReviewRepository reviewRepository, UserManager<User> userManager, IUserRepository userRepository, IEmailService emailService, ApplicationDbContext context, IServiceProvider sp): base(userManager, userRepository)
        {
            _reviewRepository = reviewRepository;
            _emailService = emailService;
            _context = context;
            _sp = sp;
        }

        public async Task<IActionResult> Index()
        {
            var avg = await _reviewRepository.GetAverageAppRatingAsync();
            var reviews = await _reviewRepository.GetAppReviewsAsync();
            var vm = reviews.OrderByDescending(r => r.CreatedAt).Select(r => new ReviewViewModel
            {
                Id = r.Id,
                Rating = r.Rating,
                Comment = r.Comment,
                Target = r.Target.ToString(),
                AgentId = r.AgentId,
                TransactionId = r.TransactionId,
                CreatedAt = r.CreatedAt,
                UserName = $"{r.User?.FirstName} {r.User?.LastName}",
                AverageRating = avg
            }).ToList();
            ViewBag.AverageRating = avg;
            return View(vm);
        }

        public async Task<IActionResult> MyReviews()
        {
            var userId = _userManager.GetUserId(User);
            var mine = await _context.Reviews
                .Include(r => r.Agent).Include(r => r.Transaction)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt).ToListAsync();

            ViewBag.MyAppReviews = mine.Where(r => r.Target == ReviewTarget.App)
                .Select(r => new ReviewViewModel { Id = r.Id, Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt, UserName = "You", Target = "App" }).ToList();

            var txs = await _context.Transactions
                .Include(t => t.SenderCurrency).Include(t => t.ReceiverCurrency)
                .Include(t => t.ReceiverWallet).ThenInclude(w => w!.User)
                .Include(t => t.SenderWallet).ThenInclude(w => w!.User)
                .Where(t => (t.SenderWallet != null && t.SenderWallet.UserId == userId)
                         || (t.ReceiverWallet != null && t.ReceiverWallet.UserId == userId))
                .OrderByDescending(t => t.CreatedAt).Take(50).ToListAsync();

            ViewBag.Transactions = txs;
            ViewBag.TransactionReviews = mine
                .Where(r => r.Target == ReviewTarget.Transaction && r.TransactionId != null)
                .ToDictionary(r => r.TransactionId!.Value);

            var agents = await _context.Agents
                .Where(a => a.Status == AgentStatus.Approved)
                .OrderBy(a => a.StoreName).ToListAsync();
            ViewBag.Agents = agents;
            ViewBag.AgentReviews = mine
                .Where(r => r.Target == ReviewTarget.Agent && r.AgentId != null)
                .ToDictionary(r => r.AgentId!.Value);

            return View();
        }

        public async Task<IActionResult> Create(int? agentId = null, int? transactionId = null)
        {
            var userId = _userManager.GetUserId(User);
            Review? existing = null;
            if (agentId.HasValue)
                existing = await _context.Reviews.FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.AgentId == agentId && r.Target == ReviewTarget.Agent);
            else if (transactionId.HasValue)
                existing = await _context.Reviews.FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.TransactionId == transactionId && r.Target == ReviewTarget.Transaction);
            else
                existing = await _context.Reviews.OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.Target == ReviewTarget.App);

            var vm = new ReviewViewModel
            {
                Id = existing?.Id ?? 0,
                Rating = existing?.Rating ?? 0,
                Comment = existing?.Comment,
                AgentId = agentId,
                TransactionId = transactionId,
                Target = agentId.HasValue ? "Agent" : transactionId.HasValue ? "Transaction" : "App"
            };

            if (agentId.HasValue)
                ViewBag.AgentInfo = await _context.Agents.FindAsync(agentId);
            if (transactionId.HasValue)
                ViewBag.TransactionInfo = await _context.Transactions
                    .Include(t => t.SenderCurrency).Include(t => t.ReceiverCurrency)
                    .FirstOrDefaultAsync(t => t.Id == transactionId);
            ViewBag.IsEdit = existing != null;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(ReviewViewModel vm)
        {
            if (vm.Rating == 0) { ModelState.AddModelError("Rating", "Please select a rating."); return View("Create", vm); }

            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            ReviewTarget target = vm.Target switch
            {
                "Agent" => ReviewTarget.Agent,
                "Transaction" => ReviewTarget.Transaction,
                _ => ReviewTarget.App
            };

            if (vm.Id > 0)
            {
                var ex = await _context.Reviews.FirstOrDefaultAsync(r => r.Id == vm.Id && r.UserId == userId);
                if (ex != null)
                {
                    ex.Rating = vm.Rating; ex.Comment = vm.Comment;
                    _context.Reviews.Update(ex);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Review updated! ⭐";
                    return RedirectToAction(nameof(MyReviews));
                }
            }

            await _reviewRepository.AddAsync(new Review
            {
                Rating = vm.Rating,
                Comment = vm.Comment,
                Target = target,
                AgentId = target == ReviewTarget.Agent ? vm.AgentId : null,
                TransactionId = target == ReviewTarget.Transaction ? vm.TransactionId : null,
                UserId = userId,
                CreatedAt = DateTime.Now
            });
            TempData["Success"] = "Thank you for your review! ⭐";

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var es = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var admins = await _userManager.GetUsersInRoleAsync("Admin");
                    foreach (var a in admins)
                        if (a.Email != null)
                            await es.SendNotificationAsync(a.Email, $"{a.FirstName} {a.LastName}",
                                "New Review", $"{user?.FirstName} gave {vm.Rating}/5 ({target}): {vm.Comment}");
                }
                catch { }
            });

            return RedirectToAction(nameof(MyReviews));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var r = await _context.Reviews.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (r != null) { _context.Reviews.Remove(r); await _context.SaveChangesAsync(); }
            TempData["Success"] = "Review deleted.";
            return RedirectToAction(nameof(MyReviews));
        }
    }
}