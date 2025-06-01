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

        // Enhanced SendNotificationToUserAsync for essential notifications
        public async Task SendNotificationToUserAsync(string userId, object payloadObject)
        {
            var vapidDetails = LoadVapidDetails();
            if (vapidDetails == null)
            {
                _logger.LogError("CRITICAL: VAPID details are null. Cannot send notification to user {UserId}.", userId);
                throw new InvalidOperationException("VAPID configuration is missing or invalid");
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
                _logger.LogError(ex, "CRITICAL: Database error fetching subscriptions for user ID {UserId}.", userId);
                throw; // Re-throw for essential notifications
            }

            if (!userSubscriptions.Any())
            {
                _logger.LogError("CRITICAL: No push subscriptions found for user ID {UserId}.", userId);
                throw new InvalidOperationException($"No push subscriptions found for user {userId}");
            }

            _logger.LogInformation("Attempting to send notification to {Count} subscriptions for user ID {UserId}.", userSubscriptions.Count, userId);

            var payloadJson = JsonSerializer.Serialize(payloadObject);
            var subscriptionsToRemove = new List<Models.PushSubscription>();
            var successfulSends = 0;
            var exceptions = new List<Exception>();

            try
            {
                foreach (var appSubscription in userSubscriptions)
                {
                    var webPushSubscription = new WebPush.PushSubscription(appSubscription.Endpoint, appSubscription.P256DH, appSubscription.Auth);
                    try
                    {
                        await SendAndHandleErrorsAsync(webPushSubscription, payloadJson, vapidDetails, appSubscription, subscriptionsToRemove, userId);
                        successfulSends++;
                        _logger.LogInformation("Successfully sent to subscription {SubscriptionId} for user {UserId}", appSubscription.Id, userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send to subscription {SubscriptionId} for user {UserId}", appSubscription.Id, userId);
                        exceptions.Add(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL: Unexpected error during sending notifications to user {UserId}", userId);
                throw;
            }

            // Clean up invalid subscriptions
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
                }
            }

            // For essential notifications, ensure at least one was sent successfully
            if (successfulSends == 0)
            {
                _logger.LogError("CRITICAL: Failed to send notification to any subscription for user {UserId}. Total attempts: {TotalAttempts}",
                    userId, userSubscriptions.Count);

                if (exceptions.Any())
                {
                    throw new AggregateException("Failed to send notification to any subscription", exceptions);
                }
                else
                {
                    throw new InvalidOperationException($"No valid subscriptions available for user {userId}");
                }
            }

            _logger.LogInformation("Successfully sent notification to {SuccessfulSends}/{TotalSubscriptions} subscriptions for user {UserId}",
                successfulSends, userSubscriptions.Count, userId);
        }
        private async Task SendAndHandleErrorsAsync(WebPush.PushSubscription webPushSub,
                                                    string payload,
                                                    VapidDetails vapid,
                                                    Models.PushSubscription appSub,
                                                    List<Models.PushSubscription> subsToRemove,
                                                    string userIdForLog = null)
        {
            string userCtx = string.IsNullOrEmpty(userIdForLog) ? "" : $" for user {userIdForLog}";
            try
            {
                // Add timeout to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _webPushClient.SendNotificationAsync(webPushSub, payload, vapid)
                    .WaitAsync(cts.Token);
                _logger.LogDebug("Successfully sent notification to endpoint: {Endpoint}{UserContext}", appSub.Endpoint, userCtx);
            }
            catch (WebPushException ex)
            {
                _logger.LogError(ex, "WebPushException sending to {Endpoint}{UserContext}. Status Code: {StatusCode}", appSub.Endpoint, userCtx, ex.StatusCode);
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound || ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    lock (subsToRemove)
                    {
                        if (!subsToRemove.Any(s => s.Id == appSub.Id))
                        {
                            subsToRemove.Add(appSub);
                        }
                    }
                }
                throw; // Re-throw to allow retry logic to work
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout sending notification to {Endpoint}{UserContext}", appSub.Endpoint, userCtx);
                throw; // Re-throw to allow retry logic to work
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error sending push notification to {Endpoint}{UserContext}", appSub.Endpoint, userCtx);
                throw; // Re-throw to allow retry logic to work
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