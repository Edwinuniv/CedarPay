using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace MoneyTransfer.Controllers
{
    public class MapController : BaseController
    {
        private readonly IAgentRepository _agentRepository;

        public MapController(IAgentRepository agentRepository, UserManager<User> userManager,
            IUserRepository userRepository) : base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
        }

        public async Task<IActionResult> Index()
        {
            var agents = await _agentRepository.GetApprovedAgentsAsync();

            if (User.IsInRole("Agent") || User.IsInRole("Admin"))
            {
                var userId = _userManager.GetUserId(User);
                var agent = await _agentRepository.GetByUserIdAsync(userId);
                if (agent != null)
                {
                    ViewBag.MyStoreLat = agent.Latitude;
                    ViewBag.MyStoreLng = agent.Longitude;
                    ViewBag.UserAgent = agent;
                }
            }

            return View(agents.ToList());
        }
    }
}