using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.ViewModels;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class ReviewController : BaseController
    {
        private readonly IReviewRepository _reviewRepository;
        private readonly UserManager<User> _userManager;

        public ReviewController(IReviewRepository reviewRepository, UserManager<User> userManager,
            IUserRepository userRepository): base(userManager, userRepository)
        {
            _reviewRepository = reviewRepository;
            _userManager = userManager;
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
            var reviews = await _reviewRepository.GetByUserIdAsync(userId);

            var vm = reviews
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewViewModel
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    Target = r.Target.ToString(),
                    CreatedAt = r.CreatedAt,
                    UserName = "You"
                }).ToList();

            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            var existing = await _reviewRepository.GetByUserIdAsync(userId);
            var lastReview = existing
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (lastReview != null)
            {
                ViewBag.LastReviewDate =
                    lastReview.CreatedAt.ToString("MMM dd, yyyy");

                return View(new ReviewViewModel
                {
                    Rating = lastReview.Rating,
                    Comment = lastReview.Comment
                });
            }

            return View(new ReviewViewModel { Rating = 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(ReviewViewModel vm)
        {
            if (vm.Rating == 0)
            {
                ModelState.AddModelError("Rating",
                    "Please select a rating.");
                return View("Create", vm);
            }

            var userId = _userManager.GetUserId(User);

            var review = new Review
            {
                Rating = vm.Rating,
                Comment = vm.Comment,
                Target = ReviewTarget.App,
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            await _reviewRepository.AddAsync(review);
            TempData["Success"] = "Thank you for your review! ⭐";

            return RedirectToAction(nameof(MyReviews));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            await _reviewRepository.DeleteAsync(id);
            TempData["Success"] = "Review deleted.";
            return RedirectToAction(nameof(MyReviews));
        }
    }
}