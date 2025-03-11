using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.Tasks;

namespace PointageZones.Controllers.Api 
{ 
    [ApiController]
    [Route("api")]
    public class HealthCheckController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;

        public HealthCheckController(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        [HttpGet("health")] //  /api/health
        [AllowAnonymous] 
        public async Task<IActionResult> HealthCheck()
        {
            var report = await _healthCheckService.CheckHealthAsync();

            if (report.Status == HealthStatus.Healthy)
            {
                return Ok("Healthy"); 
            }
            else
            {
                return StatusCode(503, "Service Unavailable"); 
            }

            //Or, for more detailed information (less necessary for a simple check)
            /*
            return Ok(new
            {
                Status = report.Status.ToString(),
                //You can add entries, but for simple check, this is enough
            });
            */
        }


       
        [HttpGet("health/minimal")] // /api/health/minimal
        [AllowAnonymous]
        public IActionResult HealthCheckMinimal()
        {
            return Ok(); // Returns 200 OK.  That's it.
        }
    }
}