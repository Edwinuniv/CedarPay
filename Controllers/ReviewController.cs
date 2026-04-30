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

        public ReviewController(IReviewRepository reviewRepository, UserManager<User> userManager, IUserRepository userRepository, IEmailService emailService, ApplicationDbContext context) : base(userManager, userRepository)
        {
            _reviewRepository = reviewRepository;
            _emailService = emailService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var avgRating = await _reviewRepository.GetAverageAppRatingAsync();
            var reviews = await _reviewRepository.GetAppReviewsAsync();

            var vm = reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    Target = r.Target.ToString(),
                    AgentId = r.AgentId,
                    TransactionId = r.TransactionId,
                    CreatedAt = r.CreatedAt,
                    UserName = $"{r.User?.FirstName} {r.User?.LastName}",
                    AverageRating = avgRating
                }).ToList();

            ViewBag.AverageRating = avgRating;
            return View(vm);
        }

        public async Task<IActionResult> MyReviews()
        {
            var userId = _userManager.GetUserId(User);
            var all = (await _reviewRepository
                .GetByUserIdAsync(userId)).ToList();

            ViewBag.MyReviews = all
                .Where(r => r.Target == ReviewTarget.App)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UserName = "You"
                }).ToList();

            ViewBag.TransactionReviews = all
                .Where(r => r.TransactionId != null)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    TransactionId = r.TransactionId
                }).ToList();

            ViewBag.AgentReviews = all
                .Where(r => r.Target == ReviewTarget.Agent)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                }).ToList();

            return View();
        }

        public async Task<IActionResult> Create(int? agentId = null, int? transactionId = null)
        {
            var userId = _userManager.GetUserId(User);
            var existing = await _reviewRepository.GetByUserIdAsync(userId);
            var lastReview = existing
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            var vm = new ReviewViewModel
            {
                Rating = 0,
                AgentId = agentId,
                TransactionId = transactionId
            };

            if (lastReview != null && agentId == null && transactionId == null)
            {
                ViewBag.LastReviewDate = lastReview.CreatedAt.ToString("MMM dd, yyyy");
                vm.Rating = lastReview.Rating;
                vm.Comment = lastReview.Comment;
            }

            if (agentId.HasValue)
                vm.Target = "Agent";
            else if (transactionId.HasValue)
                vm.Target = "Transaction";
            else
                vm.Target = "App";

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(ReviewViewModel vm)
        {
            if (vm.Rating == 0)
            {
                ModelState.AddModelError("Rating", "Please select a rating.");
                return View("Create", vm);
            }

            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            var review = new Review
            {
                Rating = vm.Rating,
                Comment = vm.Comment,
                Target = Enum.Parse<ReviewTarget>(vm.Target),
                AgentId = vm.AgentId,
                TransactionId = vm.TransactionId,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            await _reviewRepository.AddAsync(review);
            TempData["Success"] = "Thank you for your review! ⭐";

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var ratingStars = new string('⭐', vm.Rating);

            foreach (var admin in adminUsers)
            {
                if (admin.Email != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var targetInfo = "";
                        if (vm.AgentId.HasValue)
                            targetInfo = $"Agent ID: {vm.AgentId}";
                        else if (vm.TransactionId.HasValue)
                            targetInfo = $"Transaction ID: {vm.TransactionId}";
                        else
                            targetInfo = "App Review";

                        await _emailService.SendNotificationAsync(
                            admin.Email,
                            $"{admin.FirstName} {admin.LastName}",
                            "New User Review Submitted",
                            $"A new review has been submitted by {user?.FirstName} {user?.LastName}.\n\n" +
                            $"Target: {targetInfo}\n" +
                            $"Rating: {vm.Rating}/5 {ratingStars}\n" +
                            $"Comment: {vm.Comment}\n\n" +
                            $"Please check the admin panel to view all reviews.");
                    });
                }
            }

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Thank You for Your Review!",
                        $"Thank you for taking the time to share your feedback with us!\n\n" +
                        $"Your review:\n" +
                        $"Rating: {vm.Rating}/5 {ratingStars}\n" +
                        $"Comment: {vm.Comment}\n\n" +
                        $"We truly appreciate your input as it helps us improve CedarPay for everyone.");
                });
            }

            if (vm.AgentId.HasValue)
                return RedirectToAction("Details", "Agent", new { id = vm.AgentId });
            else if (vm.TransactionId.HasValue)
                return RedirectToAction("Details", "Transaction", new { id = vm.TransactionId });
            else
                return RedirectToAction(nameof(MyReviews));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _reviewRepository.DeleteAsync(id);
            TempData["Success"] = "Review deleted.";
            return RedirectToAction(nameof(MyReviews));
        }

        public async Task<IActionResult> EditReview(int id)
        {
            var userId = _userManager.GetUserId(User);
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r =>
                    r.Id == id && r.UserId == userId);

            if (review == null) return NotFound();

            var vm = new ReviewViewModel
            {
                Id = review.Id,
                Rating = review.Rating,
                Comment = review.Comment
            };

            return View("Create", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReview(
            ReviewViewModel vm)
        {
            var userId = _userManager.GetUserId(User);
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r =>
                    r.Id == vm.Id && r.UserId == userId);

            if (review == null) return NotFound();

            review.Rating = vm.Rating;
            review.Comment = vm.Comment;
            _context.Reviews.Update(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Review updated!";
            return RedirectToAction("MyReviews");
        }
    }
}