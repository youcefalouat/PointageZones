// Controllers/Api/PushController.cs
using Microsoft.AspNetCore.Authorization; // Needed if you secure endpoints
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PointageZones.Data; // Your DbContext namespace
using PointageZones.Models;
using PointageZones.Services; // Namespace for PushNotificationService
using System;
using System.Linq;
using System.Security.Claims; // For getting UserId
using System.Threading.Tasks;

namespace PointageZones.Controllers.Api
{
    public class PushSubscriptionDto // Keep this DTO
    {
        public string Endpoint { get; set; }
        public string P256DH { get; set; }
        public string Auth { get; set; }
    }

    [ApiController]
    [Route("api/push")]
    // [Authorize] // IMPORTANT: Secure these endpoints, especially subscribe/unsubscribe
    public class PushController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext; // Your DbContext
        private readonly ILogger<PushController> _logger;
        private readonly PushNotificationService _notificationService; 
        private readonly IConfiguration _configuration; 

        public PushController(ApplicationDbContext dbContext, ILogger<PushController> logger, PushNotificationService notificationService, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        [HttpGet("/api/config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                VapidPublicKey = _configuration["VapidDetails:PublicKey"]
            });
        }

        [HttpPost("subscribe")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        // [Authorize] // Make sure only logged-in users can subscribe
        public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get user ID from claims

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Subscribe attempt failed: User not authenticated.");
                return Unauthorized("User must be logged in to subscribe.");
            }

            if (dto == null || string.IsNullOrEmpty(dto.Endpoint))
            {
                _logger.LogWarning("Subscribe request for user {UserId} received with invalid data.", userId);
                return BadRequest("Invalid subscription data provided.");
            }
            try 
            { 
            _logger.LogInformation("Received subscription for endpoint: {Endpoint} from user {UserId}", dto.Endpoint, userId);

            // Check if this specific endpoint already exists for this user
            bool exists = await _dbContext.PushSubscriptions
                                        .AnyAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

            if (!exists)
            {
                // Fix for CS0128 and CS9035: Ensure unique variable name and initialize all required properties
                var newSubscription = new PushSubscription
                {
                    UserId = userId,
                    Endpoint = dto.Endpoint,
                    P256DH = dto.P256DH,
                    Auth = dto.Auth,
                    CreatedAt = DateTime.UtcNow // Initialize CreatedAt or any other required property
                };

                _dbContext.PushSubscriptions.Add(newSubscription);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Subscription added for user {UserId}.", userId);
            }
            else
            {
                _logger.LogInformation("Subscription endpoint already exists for user {UserId}.", userId);

                // Mettre à jour l'abonnement existant pour s'assurer que les clés sont à jour
                var existingSubscription = await _dbContext.PushSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

                if (existingSubscription != null)
                {
                    existingSubscription.P256DH = dto.P256DH;
                    existingSubscription.Auth = dto.Auth;
                    existingSubscription.CreatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Abonnement existant mis à jour pour l'utilisateur {UserId}", userId);
                }
            }

            return Ok(new
            {
                Success = true,
                Message = "Abonnement enregistré avec succès.",
                
            });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'enregistrement de l'abonnement pour l'utilisateur {UserId}", userId);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Une erreur s'est produite lors de l'enregistrement de l'abonnement."
                });
            }
        }

        [HttpPost("unsubscribe")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        // [Authorize] // Ensure user is logged in
        public async Task<IActionResult> Unsubscribe([FromBody] PushSubscriptionDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get user ID

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unsubscribe attempt failed: User not authenticated.");
                return Unauthorized("User must be logged in to unsubscribe.");
            }

            if (dto == null || string.IsNullOrEmpty(dto.Endpoint))
            {
                _logger.LogWarning("Unsubscribe request for user {UserId} received with invalid data.", userId);
                return BadRequest("Invalid subscription data provided.");
            }

            _logger.LogInformation("Received unsubscribe request for endpoint: {Endpoint} from user {UserId}", dto.Endpoint, userId);

            var subscriptionToRemove = await _dbContext.PushSubscriptions
                                         .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == dto.Endpoint);

            if (subscriptionToRemove != null)
            {
                _dbContext.PushSubscriptions.Remove(subscriptionToRemove);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Subscription removed for user {UserId}.", userId);
            }
            else
            {
                _logger.LogWarning("Subscription not found for unsubscribe: {Endpoint} for user {UserId}", dto.Endpoint, userId);
            }

            return Ok(); // Return OK even if not found
        }

        

        // Optional: Endpoint to trigger a test notification to the CALLING user (for testing subscription)
        [HttpPost("notify-me-test")]
        // [Authorize]
        public async Task<IActionResult> NotifyMeTestSubscriber()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            _logger.LogInformation("Executing NotifyMeTestSubscriber endpoint for user {UserId}.", userId);
            var payload = new { title = "Test Notification (Me)", body = "This is a test push notification for your user.", url = "/" };
            await _notificationService.SendNotificationToUserAsync(userId, payload);
            return Ok(new { Message = "Test notification sent to your active subscriptions." });
        }

        [HttpGet("vapid-public-key")]
        public IActionResult GetVapidPublicKey()
        {
            var vapidPublicKey = _configuration["VapidDetails:PublicKey"];

            if (string.IsNullOrEmpty(vapidPublicKey))
            {
                _logger.LogError("VAPID public key is missing in configuration.");
                return NotFound();
            }

            return Ok(new { publicKey = vapidPublicKey });
        }
    }

}