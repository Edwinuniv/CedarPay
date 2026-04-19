using Microsoft.AspNetCore.Mvc;
using MoneyTransfer.Repositories.Interfaces;

namespace MoneyTransfer.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentApiController : ControllerBase
    {
        private readonly IAgentRepository _agentRepository;

        public AgentApiController(IAgentRepository agentRepository)
        {
            _agentRepository = agentRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetApprovedAgents()
        {
            var agents = await _agentRepository.GetApprovedAgentsAsync();

            var result = agents.Select(a => new
            {
                a.Id,
                a.StoreName,
                a.AgentName,
                a.City,
                a.Country,
                a.Latitude,
                a.Longitude,
                a.WorkingHours,
                a.PhoneNumber,
                a.Email
            });

            return Ok(result);
        }

        [HttpGet("city/{city}")]
        public async Task<IActionResult> GetByCity(string city)
        {
            var agents = await _agentRepository.GetByCityAsync(city);

            if (!agents.Any())
                return NotFound(new { message = $"No agents found in {city}." });

            return Ok(agents.Select(a => new
            {
                a.Id,
                a.StoreName,
                a.City,
                a.Latitude,
                a.Longitude,
                a.WorkingHours
            }));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAgent(int id)
        {
            var agent = await _agentRepository.GetAgentWithCommissionsAsync(id);

            if (agent == null)
                return NotFound(new { message = "Agent not found." });

            return Ok(new
            {
                agent.Id,
                agent.StoreName,
                agent.AgentName,
                agent.Description,
                agent.City,
                agent.Country,
                agent.WorkingHours,
                agent.PhoneNumber,
                agent.Email,
                agent.Latitude,
                agent.Longitude,
                ReviewCount = agent.Reviews?.Count ?? 0
            });
        }
    }
}