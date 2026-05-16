using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Constants;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using MoneyTransfer.Services.Interfaces;

namespace MoneyTransfer.Controllers
{
    [Authorize]
    public class AgentApplicationController : BaseController
    {
        private readonly IAgentApplicationRepository _applicationRepository;
        private readonly IAgentRepository _agentRepository;
        private readonly IEmailService _emailService;

        public AgentApplicationController(IAgentApplicationRepository applicationRepository, IAgentRepository agentRepository, UserManager<User> userManager, IUserRepository userRepository, IEmailService emailService) : base(userManager, userRepository)
        {
            _applicationRepository = applicationRepository;
            _agentRepository = agentRepository;
            _emailService = emailService;
        }

        public async Task<IActionResult> Apply()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (User.IsInRole(Roles.Agent))
            {
                TempData["Error"] = "You are already an agent.";
                return RedirectToAction("Index", "Dashboard");
            }

            var hasPending = await _applicationRepository
                .HasPendingApplicationAsync(userId);

            if (hasPending)
            {
                TempData["Error"] =
                    "You already have a pending application. " +
                    "Please wait for admin review.";
                return RedirectToAction("MyApplication");
            }

            var lastApp = (await _applicationRepository.GetByUserIdAsync(userId))
                .OrderByDescending(a => a.SubmittedAt)
                .FirstOrDefault();

            if (lastApp != null && lastApp.Status == ApplicationStatus.Rejected)
            {
                ViewBag.RejectionReason = lastApp.RejectionReason;
                return View(new AgentApplication
                {
                    AgentName = lastApp.AgentName,
                    StoreName = lastApp.StoreName,
                    PhoneNumber = lastApp.PhoneNumber,
                    Email = lastApp.Email,
                    Street = lastApp.Street,
                    City = lastApp.City,
                    Region = lastApp.Region,
                    Country = lastApp.Country,
                    WorkingHours = lastApp.WorkingHours,
                    Description = lastApp.Description
                });
            }

            return View(new AgentApplication());
        }

        [HttpPost]
        public async Task<IActionResult> Apply(AgentApplication application)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            ModelState.Remove("UserId");
            ModelState.Remove("User");

            if (!ModelState.IsValid)
            {
                return View(application);
            }

            var user = await _userManager.FindByIdAsync(userId);

            application.UserId = userId;
            application.Status = ApplicationStatus.Pending;
            application.SubmittedAt = DateTime.Now;

            await _applicationRepository.AddAsync(application);

            if (user?.Email != null)
            {
                _ = Task.Run(async () =>
                {
                    await _emailService.SendNotificationAsync(
                        user.Email,
                        $"{user.FirstName} {user.LastName}",
                        "Agent Application Received",
                        $"Thank you for submitting your agent application for '{application.StoreName}'. Our team will review your application and notify you within 1-2 business days.\n\n" +
                        $"Application Summary:\n" +
                        $"• Store Name: {application.StoreName}\n" +
                        $"• Agent Name: {application.AgentName}\n" +
                        $"• Email: {application.Email}\n" +
                        $"• Phone: {application.PhoneNumber}\n\n" +
                        $"You can check the status of your application anytime from your profile page.");
                });
            }

            TempData["Success"] = "Your agent application has been submitted! You will be notified once reviewed.";

            return RedirectToAction("MyApplication");
        }

        public async Task<IActionResult> MyApplication()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var applications = await _applicationRepository.GetByUserIdAsync(userId);

            return View(applications);
        }
    }
}