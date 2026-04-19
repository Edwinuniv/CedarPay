using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Models;
using MoneyTransfer.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace MoneyTransfer.Controllers
{
    public class MapController : BaseController
    {
        private readonly IAgentRepository _agentRepository;

        public MapController(IAgentRepository agentRepository,UserManager<User> userManager,
            IUserRepository userRepository): base(userManager, userRepository)
        {
            _agentRepository = agentRepository;
        }

        public async Task<IActionResult> Index()
        {
            var agents = await _agentRepository.GetApprovedAgentsAsync();
            return View(agents.ToList());
        }
    }
}