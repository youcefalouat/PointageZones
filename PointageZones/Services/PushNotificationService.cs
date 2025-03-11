// Services/PushNotificationService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PointageZones.Data; 
using PointageZones.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebPush;

namespace PointageZones.Services
{
    public class PushNotificationService
    {
        private readonly WebPushClient _webPushClient;
        private readonly ApplicationDbContext _dbContext; 
        private readonly IConfiguration _configuration;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(ApplicationDbContext dbContext, IConfiguration configuration, ILogger<PushNotificationService> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
            _webPushClient = new WebPushClient(); // Instantiate here or inject if registered as Singleton/Scoped
        }

        public async Task SendNotificationToUserAsync(string userId, object payloadObject)
        {
            var vapidDetails = LoadVapidDetails();
            if (vapidDetails == null)
            {
                _logger.LogError("VAPID details are null. Cannot send notification to user {UserId}.", userId);
                return; // Critical: If VAPID details missing, can't proceed.
            }

            List<Models.PushSubscription> userSubscriptions;
            try
            {
                userSubscriptions = await _dbContext.PushSubscriptions
                                                .Where(s => s.UserId == userId)
                                                .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database error fetching subscriptions for user ID {UserId}. Notification might not be sent.", userId);
                // For a critical notification, you might want to rethrow or return a specific error indicator.
                // For now, let's assume logging and returning is the policy.
                return;
            }

            if (!userSubscriptions.Any())
            {
                _logger.LogInformation("No push subscriptions found for user ID {UserId}.", userId);
                return;
            }

            _logger.LogInformation("Attempting to send notification to {Count} subscriptions for user ID {UserId}.", userSubscriptions.Count, userId);

            var payloadJson = JsonSerializer.Serialize(payloadObject);
            var tasks = new List<Task>();
            var subscriptionsToRemove = new List<Models.PushSubscription>(); // Your existing list

            foreach (var appSubscription in userSubscriptions)
            {
                var webPushSubscription = new WebPush.PushSubscription(appSubscription.Endpoint, appSubscription.P256DH, appSubscription.Auth);
                // Call the same helper method used in SendNotificationToAllAsync, passing userId for context
                tasks.Add(SendAndHandleErrorsAsync(webPushSubscription, payloadJson, vapidDetails, appSubscription, subscriptionsToRemove, userId));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                // This catch block is unlikely to be hit if SendAndHandleErrorsAsync catches all its exceptions.
                // However, it's a safeguard for issues with Task.WhenAll itself or if SendAndHandleErrorsAsync rethrows.
                _logger.LogError(ex, "An unexpected error occurred during Task.WhenAll while sending notifications to user {UserId}", userId);
            }

            if (subscriptionsToRemove.Any())
            {
                _logger.LogWarning("Removing {Count} invalid subscriptions for user {UserId}.", subscriptionsToRemove.Count, userId);
                try
                {
                    _dbContext.PushSubscriptions.RemoveRange(subscriptionsToRemove);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error removing subscriptions for user {UserId}.", userId);
                    // Consider if this failure needs to be propagated for a "critical" notification system
                }
            }
        }

        // Ensure SendAndHandleErrorsAsync can accept userId for logging context (optional parameter)
        private async Task SendAndHandleErrorsAsync(WebPush.PushSubscription webPushSub,
                                                    string payload,
                                                    VapidDetails vapid,
                                                    Models.PushSubscription appSub,
                                                    List<Models.PushSubscription> subsToRemove,
                                                    string userIdForLog = null) // Make userId optional for broader use
        {
            string userCtx = string.IsNullOrEmpty(userIdForLog) ? "" : $" for user {userIdForLog}";
            try
            {
                await _webPushClient.SendNotificationAsync(webPushSub, payload, vapid);
                _logger.LogDebug("Successfully sent notification to endpoint: {Endpoint}{UserContext}", appSub.Endpoint, userCtx);
            }
            catch (WebPushException ex)
            {
                _logger.LogError(ex, "WebPushException sending to {Endpoint}{UserContext}. Status Code: {StatusCode}", appSub.Endpoint, userCtx, ex.StatusCode);
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    lock (subsToRemove)
                    {
                        // Check if already added, though less likely in this flow per user
                        if (!subsToRemove.Any(s => s.Id == appSub.Id)) // Assuming PushSubscription has an Id PK
                        {
                            subsToRemove.Add(appSub);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error sending push notification to {Endpoint}{UserContext}", appSub.Endpoint, userCtx);
            }
        }

        private VapidDetails LoadVapidDetails()
        {
            var vapidSubject = _configuration["VapidDetails:Subject"];
            var vapidPublicKey = _configuration["VapidDetails:PublicKey"];
            var vapidPrivateKey = _configuration["VapidDetails:PrivateKey"];

            if (string.IsNullOrEmpty(vapidSubject) || string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
            {
                _logger.LogError("VAPID details are missing in configuration.");
                return null;
            }
            return new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        }
    }
}