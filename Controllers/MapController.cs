using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Data;

namespace MoneyTransfer.Controllers
{
    public class MapController : BaseController
    {
        private readonly IAgentRepository _agentRepository;
        private readonly ApplicationDbContext _context;

        public MapController(IAgentRepository agentRepository, UserManager<User> userManager, IUserRepository userRepository, ApplicationDbContext context): base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var agents = await _context.Agents
                .Include(a => a.Reviews)
                .Where(a => a.Status == AgentStatus.Approved)
                .ToListAsync();

            foreach (var agent in agents)
            {
                var agentReviews = agent.Reviews.Where(r => r.Target == ReviewTarget.Agent);
                agent.AverageRating = agentReviews.Any() ? agentReviews.Average(r => r.Rating) : 0;
                agent.ReviewCount = agentReviews.Count();
            }

            if (User.IsInRole("Agent") || User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                var userAgents = await _agentRepository.GetByUserIdAsync(userId);
                ViewBag.UserAgents = userAgents.ToList();
            }

            return View(agents.OrderByDescending(a => a.AverageRating).ToList());
        }
    }
}