using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PointageZones.Areas.Identity.Data;
using PointageZones.Data;
using PointageZones.DTO;
using PointageZones.Models;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using NuGet.Protocol.Core.Types;
using PointageZones.Services;

namespace PointageZones.Controllers
{
    [Authorize]
    public class AgentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AgentController> _logger;
        private readonly PushNotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly DateTime now;

        public AgentController(ApplicationDbContext context, ILogger<AgentController> logger, PushNotificationService notificationService, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
            _configuration = configuration;
            now = DateTime.Now;//.Date.AddHours(00).AddMinutes(00);
        }
        
        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                var online = await CheckDatabaseConnection();
                if (online)
                {
                    ViewBag.VapidPublicKey = _configuration["VapidDetails:PublicKey"];
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    var pointagesIncomplets = await _context.Pointages
                           .Include(p => p.PlanTour)
                            .ThenInclude(pt => pt.Tour)
                           .Where(p => p.UserId == userId && p.IsChecked == 0 
                           && ((p.DateTimeDebTour.HasValue && p.DateTimeDebTour.Value.Date == now.Date)
                           || (p.DateTimeFinTour.HasValue && p.DateTimeFinTour.Value.Date == now.Date)))
                           .ToListAsync();

                    foreach (var pointage in pointagesIncomplets)
                    {                        
                        if (pointage.DateTimeDebTour < now && pointage.DateTimeFinTour >= now)
                            {
                            // Get the tour details needed for the notification
                            int tourId = pointage.PlanTour.TourId;
                            string tourRef = pointage.PlanTour.Tour.RefTour;

                            var notificationPayload = new
                            {
                                title = "Tournée Active",
                                body = $"La tournée ({tourRef}) est active. Démarrage en cours.",
                                url = Url.Action("DebutTour", "Agent", new { id = tourId }, Request.Scheme),
                                icon = "/images/logo_mini_s_192x192.png",
                                data = new
                                {
                                    tourId = tourId,
                                    action = "debutTour",
                                    timestamp = DateTime.UtcNow
                                }
                            };

                            try
                            {
                                _logger.LogInformation("Attempting to send notification directly for user {UserId}, tour {TourId}", userId, tourId);
                                // Directly await the notification sending
                                await _notificationService.SendNotificationToUserAsync(userId, notificationPayload);
                                _logger.LogInformation("Push notification sent directly (awaited) to user {UserId} for active tour {TourId}.", userId, tourId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send push notification (awaited) to user {UserId} for tour {TourId}", userId, tourId);
                                // IMPORTANT: Decide if a notification failure should prevent the redirect.
                                // If not, this catch block is sufficient. If it should, you might rethrow or handle differently.

                                TempData["Notification"] = "Notification non envoyé";
                            }

                            return RedirectToAction("DebutTour","Agent" ,new { id = pointage.PlanTour.TourId });
                            }
                        
                    }

                    return View(await _context.Tours.ToListAsync() ?? new List<Tour>());
                }
                else
                {
                    return View("OfflineIndex");
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Erreur");
                TempData["Notification"] = "Erreur innattendu";
                return View("OfflineIndex");
            }
        }

        private async Task<bool> CheckDatabaseConnection()
        {
            try
            {
                using var cancellation = new CancellationTokenSource();
                cancellation.CancelAfter(1000);

                
                await _context.Database.ExecuteSqlRawAsync("SELECT 1",cancellation.Token);
                return true;
            }
            catch (Exception ex) 
            {
                return false;
            }
        }

        public async Task<IActionResult> DebutTour(int? id)
{
            if (id == null)
            {
                TempData["Notification"] = "Erreur dans la tournée";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var online = await CheckDatabaseConnection();
                if (!online)
                {
                    TempData["Notification"] = "Mode hors ligne activé. Certaines fonctionnalités peuvent être limitées.";

                    ViewBag.IsOfflineMode = true;
                    ViewBag.TourId = id;

                    return View();
                }
                else
                {
                    // 🔍 Récupérer la tournée avec ses `PlanTours` et `Zones`
                    var tour = await _context.Tours
                                .Include(t => t.PlanTours)
                                .ThenInclude(pt => pt.Zone)
                                .FirstOrDefaultAsync(t => t.Id == id);

                    if (tour == null || !tour.PlanTours.Any())
                    {
                        TempData["Notification"] = "la tournée est null ou pas de zones pour cette tournée";
                        return RedirectToAction(nameof(Index));
                    }

                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var role = User.FindFirstValue(ClaimTypes.Role);
                    if (string.IsNullOrEmpty(userId))
                    {
                        TempData["Notification"] = "Erreur dans la tournée";
                        return RedirectToAction(nameof(Index));
                    }

                    if (role == "admin" || role == "chef")
                    {
                        var checkResult = await checkTourPointee(id);

                        if (tour.DebTour > TimeOnly.FromDateTime(now))
                        {
                            TempData["Notification"] = checkResult.message != null ? checkResult.message : "Erreur dans la tournée";
                            return RedirectToAction(nameof(Index));
                        }
                        if (checkResult.tourPointee == true)
                        {
                            TempData["Notification"] = "La tournée a été faite. Réessayez " + (checkResult.countdown != null ? "dans " + checkResult.countdown : "demain");
                            return RedirectToAction(nameof(Index));
                        }

                        if (checkResult == null || checkResult.success == false || (checkResult.success == false && checkResult.tourPointee == false))
                        {
                            TempData["Notification"] = checkResult?.message ?? "Erreur dans la tournée";
                            return RedirectToAction(nameof(Index));
                        }

                        // 🔍 Récupérer les `ZoneId` déjà pointés pour cette tournée en cours
                        var zonesPointees = checkResult.zonePointée;
                        if (zonesPointees != null && zonesPointees.Count != 0)
                        {
                             // 🔍 Exclure les zones déjà pointées de la liste des `PlanTours`
                            var planToursNonPointes = tour.PlanTours
                                                          .Where(pt => !zonesPointees.Contains(pt.ZoneId))
                                                          .ToList();

                            if (!planToursNonPointes.Any())
                            {
                                TempData["Notification"] = "Toutes les zones de cette tournée ont déjà été pointées.";
                                return RedirectToAction(nameof(Index));
                            }
                            // Passer uniquement les `PlanTours` non pointés à la vue
                            ViewBag.Tour = tour;
                            return View(planToursNonPointes);
                        }
                        else
                        {
                            var users = await (from u in _context.Users
                                               join ur in _context.UserRoles on u.Id equals ur.UserId into userRoles
                                               from ur in userRoles.DefaultIfEmpty()
                                               join r in _context.Roles on ur.RoleId equals r.Id into roles
                                               from r in roles.DefaultIfEmpty()
                                               where (r == null || (r.Name != "admin" && r.Name != "chef")) && u.IsLocked != true
                                               select u.UserName).ToListAsync();
                            ViewBag.Tour = tour;
                            ViewBag.Users = users;
                            var planTours = tour.PlanTours.ToList();
                            return View(planTours);
                        }
                    }
                    else
                    {
                        
                        // Récupérer les pointages de l'utilisateur pour cette tournée
                        var pointages = await _context.Pointages
                            .Include(p => p.PlanTour)
                            .Where(p => p.UserId == userId && p.PlanTour.TourId == id && p.IsChecked == 0 &&
                                   ((p.DateTimeDebTour.HasValue && p.DateTimeDebTour.Value.Date == now.Date)
                           || (p.DateTimeFinTour.HasValue && p.DateTimeFinTour.Value.Date == now.Date)))
                            .ToListAsync();

                        if (!pointages.Any())
                        {
                            TempData["Notification"] = "Aucun pointage assigné pour cette tournée";
                            return RedirectToAction(nameof(Index));
                        }

                        // Vérifier la disponibilité de la tournée selon sa fréquence
                        var pointagesIncomplets = pointages
                            .Where(p => p.DateTimeDebTour < now
                                    && p.DateTimeFinTour >= now).ToList();

                        if (!pointagesIncomplets.Any())
                        {
                            TempData["Notification"] = "Tous les pointages pour cette tournée ont déjà été effectués";
                            return RedirectToAction(nameof(Index));
                        }

                        // Afficher les zones à pointer
                        ViewBag.Tour = tour;

                        // Récupérer les zones déjà pointées
                        var zonesPointees = pointages
                            .Where(p => p.IsChecked == 1)
                            .Select(p => p.PlanTour.ZoneId)
                            .ToList();

                        // Exclure les zones déjà pointées
                        var planToursNonPointes = tour.PlanTours
                            .Where(pt => !zonesPointees.Contains(pt.ZoneId))
                            .ToList();

                        return View(planToursNonPointes);

                    }
                }
            }
            catch (Exception ex) when (ex is SqlException || ex is Win32Exception)
            {
                
                TempData["Notification"] = "Mode hors ligne activé. Certaines fonctionnalités peuvent être limitées.";

                ViewBag.IsOfflineMode = true;
                ViewBag.TourId = id;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Notification"] = "Une erreur inattendue s'est produite.";
                return RedirectToAction(nameof(Index));
            }
                
}

        // GET: Agent/TourDetails/{tourId}
        //[HttpGet("GetTourDetails/{tourId}")]
        public async Task<ActionResult<object>> GetTourDetails(int tourId)
        {
            var tour = await _context.Tours
                            .Include(t => t.PlanTours)
                            .ThenInclude(pt => pt.Zone)
                            .FirstOrDefaultAsync(t => t.Id == tourId);

            if (tour == null || !tour.PlanTours.Any())
            {

                return NotFound(new { message = "Tournée non trouvée." });
            }

            
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Utilisateur non identifié." });
            }

            try
            {

                //DateTime? lastTourTime = await TourActuelleAsync(tour.Id);

                //if (lastTourTime != null)
                //{
                //    if (now > lastTourTime.Value.AddMinutes(tour.FrqTourMin.HasValue ? tour.FrqTourMin.Value : 1440))
                //    {
                //        return NotFound(new { message = "Tournée dépassé." });
                //    }
                //}

                // Logique complète pour les utilisateurs standards (vérifier les assignations réelles)
                //    var pointages = await _context.Pointages
                //        .Where(p => p.PlanTour.TourId == id &&
                //                   zonesTournee.Contains(p.PlanTour.ZoneId) &&
                //                   ((p.DateTimeScan.HasValue &&
                //                     p.DateTimeScan >= startTime &&
                //                     p.DateTimeScan <= endTime) ||
                //                    (p.DateTimeDebTour.HasValue &&
                //                     p.DateTimeFinTour.HasValue &&
                //                     p.DateTimeDebTour >= startTime &&
                //                     p.DateTimeFinTour <= endTime)))
                //        .Select(p => new {
                //            ZoneId = p.PlanTour.ZoneId,
                //            UserId = p.UserId,
                //            IsScanned = p.DateTimeScan.HasValue && p.IsChecked == 1 &&
                //                       p.DateTimeScan >= startTime && p.DateTimeScan <= endTime,
                //            IsAssigned = (p.IsChecked == 0 || p.DateTimeAssign.HasValue) 
                //                        && p.DateTimeDebTour >= startTime && p.DateTimeFinTour <= endTime
                //        })
                //        .ToListAsync();

                var pendingPointages = await _context.Pointages
                    .Include(pa => pa.PlanTour)
                    .Where(pa => pa.PlanTour.TourId == tourId
                        && pa.UserId == userId
                        && pa.IsChecked == 0
                        && pa.DateTimeDebTour < now
                        && pa.DateTimeFinTour >= now
                        //&& pa.DateTimeDebTour == lastTourTime
                        //&& pa.DateTimeFinTour == (lastTourTime.HasValue ? lastTourTime.Value.AddMinutes(tour.FrqTourMin ?? 1440) : (DateTime?)null)
                        )
                    .ToListAsync();

                // 3. Gérer le cas où il n'y a rien à pointer
                if (!pendingPointages.Any())
                {
                    // Vérifier si la tournée existe pour l'utilisateur pour distinguer les cas
                    var tourExistsForUser = await _context.Pointages
                                                .AnyAsync(pa => pa.PlanTour.TourId == tourId && pa.UserId == userId);

                    if (tourExistsForUser)
                    {
                        // La tournée existe mais est terminée pour cet utilisateur
                        var tourInfo = await _context.Tours
                                                .Where(t => t.Id == tourId)
                                                .Select(t => t.RefTour) // Sélectionner seulement RefTour
                                                .FirstOrDefaultAsync();
                        // Retourner un objet anonyme indiquant que la tournée est terminée
                        return Ok(new
                        {
                            TourId = tourId,
                            RefTour = tourInfo ?? "Tournée Inconnue",
                            Zones = new List<object>() // Retourner une liste vide pour 'zones'
                        });
                    }
                    else
                    {
                        // Tournée non trouvée ou non assignée
                        return NotFound(new { message = "Tournée non trouvée ou non assignée à cet utilisateur." });
                    }
                }



                var result = new // Définition de l'objet anonyme à retourner
                {
                    TourId = tourId,
                    RefTour = tour.RefTour,
                    // Créer la liste des zones en projetant les infos nécessaires
                    Zones = pendingPointages.Select(pa => new // Objet anonyme pour chaque zone
                    {
                        pointageId = pa.Id,
                        PlanTourId = pa.PlanTourId, 
                        ZoneId = pa.PlanTour.ZoneId,
                        RefZone = pa.PlanTour.Zone.RefZone,                        
                    }).ToList()
                };

                // 5. Retourner le résultat (sera sérialisé en JSON automatiquement par ASP.NET Core)
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur API GetTourDetails pour TourId {TourId}, UserId {UserId}", tourId, userId);
                // En cas d'erreur BDD ou autre, retourner une erreur 500 générique
                return StatusCode(500, new { message = "Une erreur interne du serveur est survenue." });
            }
        }
        [Authorize(Roles = "chef,admin")]
        public async Task<ActionResult> AssignObs(int tourId,int observation, DateTime? debTour, DateTime? finTour)
        {
            try
            {
                string returnUrl = Request.Headers["Referer"].ToString();

                var tour = await _context.Tours
                                .Include(t => t.PlanTours)
                                .ThenInclude(pt => pt.Zone)
                                .FirstOrDefaultAsync(t => t.Id == tourId);

                if (tour == null || !tour.PlanTours.Any())
                {
                    TempData["Notification"] = "la tournée est null ou pas de zones pour cette tournée";
                    return RedirectToAction(nameof(Index));
                }

                string? usernameObs = User.Identity?.Name;
                var userObs = await _context.Users.Where(u => u.IsLocked != true && u.UserName == usernameObs).FirstOrDefaultAsync();

                if (userObs == null)
                {
                    TempData["Notification"] = "Erreur utilisateur chef groupe vous n'existé pas.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }

               

                var pointages = await _context.Pointages
                .Where(p => p.PlanTour.TourId == tourId
                        && p.DateTimeDebTour == debTour
                        && p.DateTimeFinTour == finTour
                        && p.IsChecked == 0
                        && p.IsValid == 0)
                 .ToListAsync();

                if (pointages != null)
                {
                    // Sauvegarder le pointage dans une transaction pour garantir l'intégrité
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            foreach(var item in pointages)
                            {
                                item.Ref_User_Update = userObs.UserName;
                                item.ObservationId = observation;
                                item.Last_Update = now;
                                _context.Update(item);
                            }
                                                        
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            TempData["Notification"] = $"La tournée a été modifiée avec succès .";
                            //return RedirectToAction(nameof(Index));
                            return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Erreur lors de l'enregistrement des pointages pour la tournée ID {TourId}", tourId);
                            TempData["Notification"] = "Une erreur s'est produite lors de l'affectation de la tournée.";
                            return RedirectToAction(nameof(Index));
                        }
                    }
                }
                else {
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur générale dans AssignObs");
                TempData["Notification"] = "Une erreur inattendue s'est produite.";
                return RedirectToAction(nameof(Index));
            }
        }
        
        [Authorize(Roles = "chef,admin")]
        public async Task<ActionResult> AssignTour(int tourId, string username, DateTime? debTour, DateTime? finTour)
        {
            try
            {
                string returnUrl = Request.Headers["Referer"].ToString();

                var tour = await _context.Tours
                                .Include(t => t.PlanTours)
                                .ThenInclude(pt => pt.Zone)
                                .FirstOrDefaultAsync(t => t.Id == tourId);

                if (tour == null || !tour.PlanTours.Any())
                {
                    TempData["Notification"] = "la tournée est null ou pas de zones pour cette tournée";
                    return RedirectToAction(nameof(Index));
                }

                string? usernameAssign = User.Identity?.Name;
                var userAssign = await _context.Users.Where(u => u.IsLocked != true && u.UserName == usernameAssign).FirstOrDefaultAsync();

                if (userAssign == null)
                {
                    TempData["Notification"] = "Erreur utilisateur chef groupe vous n'existé pas.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrEmpty(username))
                {
                    TempData["Notification"] = "Aucun agent n'a été sélectionné.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }


                // Fetch the user once
                var user = await _context.Users.Where(u => u.IsLocked != true).FirstOrDefaultAsync(u => u.UserName == username);
                if (user == null)
                {
                    TempData["Notification"] = "L'agent sélectionné n'existe pas.";
                    return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                }

                // Start a single transaction for all operations
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var item in tour.PlanTours)
                        {
                            // Check if a pointage already exists with the same PlanTourId, DateTimeDebTour and DateTimeFinTour
                            var existingPointage = await _context.Pointages.FirstOrDefaultAsync(p =>
                                p.PlanTourId == item.Id &&
                                p.DateTimeDebTour == debTour &&
                                p.DateTimeFinTour == finTour);

                            if (existingPointage != null)
                            {
                                // Update existing pointage
                                existingPointage.UserId = username;
                                existingPointage.User = user;
                                existingPointage.Ref_User_Assign = userAssign?.UserName;
                                existingPointage.Last_Update = now;

                                _context.Pointages.Update(existingPointage);
                            }
                            else
                            {
                                var pointage = new PointageAgent
                                {
                                    UserId = username,
                                    User = user,
                                    PlanTourId = item.Id,
                                    PlanTour = item,
                                    IsChecked = 0,
                                    DateTimeScan = null,
                                    DateTimeAssign = now,
                                    DateTimeDebTour = debTour,
                                    DateTimeFinTour = finTour,
                                    Ref_User_Assign = userAssign?.UserName,
                                    Last_Update = now,
                                };

                                _context.Pointages.Add(pointage);
                            }
                        }
                        // Save all changes to the database
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["Notification"] = $"La tournée a été affectée avec succès à l'utilisateur {username}.";
                        //return RedirectToAction(nameof(Index));
                        return !string.IsNullOrEmpty(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        // Rollback the transaction in case of an error
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Erreur lors de l'enregistrement des pointages pour la tournée ID {TourId}", tourId);
                        TempData["Notification"] = "Une erreur s'est produite lors de l'affectation de la tournée.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur générale dans AssignTour pour l'utilisateur {Username}", username);
                TempData["Notification"] = "Une erreur inattendue s'est produite.";
                return RedirectToAction(nameof(Index));
            }
        }
        [Authorize(Roles = "chef,admin")]
        public async Task<ActionResult> TourDuJour(int? id, DateTime? date)
        {
            try
            {
                // Get list of all tours for the dropdown
                var allTours = await _context.Tours.OrderBy(t => t.RefTour).ToListAsync();
                ViewBag.Tours = allTours;

                // If no id is provided, just show the selection form
                if (!id.HasValue)
                {
                    ViewBag.SelectedDate = date ?? (now.Hour < 9 ? now.Date.AddDays(-1) : now.Date);
                    return View(new List<TourDuJourViewModel>());
                }

                var tour = await _context.Tours.FirstOrDefaultAsync(pt => pt.Id == id);
                if (tour == null)
                {
                    TempData["Notification"] = "Erreur tournée.";
                    return RedirectToAction(nameof(Index));
                }

                // Get today's date (or use DateTime.Today)
                var today = date ?? (now.Hour < 9 ? now.Date.AddDays(-1) : now.Date);

                ViewBag.SelectedDate = today;

                // 2. Calculate all the time slots for this tour today
                var tourSlots = new List<TourDuJourViewModel>();

                // Calculate total number of tours for the day
                int totalSlots = 0;
                if (!tour.FinTour.HasValue || !tour.FrqTourMin.HasValue || tour.FrqTourMin.Value <= 0)
                {
                    totalSlots = tour.FrqTourMin.HasValue && tour.FrqTourMin.Value < 0 ? 0 : 1;
                }
                else
                {
                    int debutMinutes = tour.DebTour.Hour * 60 + tour.DebTour.Minute;
                    int finMinutes = tour.FinTour.Value.Hour * 60 + tour.FinTour.Value.Minute;
                    int dureeMinutes = finMinutes <= debutMinutes ? (1440 - debutMinutes + finMinutes) : (finMinutes - debutMinutes);
                    totalSlots = (int)Math.Ceiling((double)dureeMinutes / tour.FrqTourMin.Value);
                }

                // Generate each time slot
                for (int i = 0; i < totalSlots; i++)
                {
                    // Calculate start and end times for this slot
                    var startTime = today.AddHours(tour.DebTour.Hour)
                                        .AddMinutes(tour.DebTour.Minute + i * (tour.FrqTourMin ?? 0));
                    var endTime = (tour.FinTour == null || tour.FrqTourMin == null) ? startTime.AddMinutes(1440) : startTime.AddMinutes(tour.FrqTourMin.Value);


                    // Get the zones that should be visited during this tour
                    var zonesTournee = await _context.PlanTours
                        .Where(pt => pt.TourId == tour.Id)
                        .Select(pt => pt.ZoneId)
                        .ToListAsync();

                    var pointages = await _context.Pointages
                        .Include(p => p.PlanTour)
                        .Include(p => p.User)
                        .Include(p => p.Observation)
                        .Where(p => p.PlanTour.TourId == tour.Id && p.DateTimeDebTour >= startTime && p.DateTimeFinTour <= endTime)
                        .OrderBy(p => p.DateTimeDebTour)
                        .ToListAsync();

                    var zonesPointees = pointages.Where(p => p.IsChecked == 1 && p.DateTimeScan.HasValue && p.DateTimeScan >= startTime && p.DateTimeScan <= endTime).Select(p => p.PlanTour.ZoneId).Distinct().ToList();
                    // Check if all required zones were scanned during this time slot
                    bool tourComplete = zonesTournee.Count > 0 &&
                                       zonesTournee.All(z => zonesPointees.Contains(z));


                    // First and last pointage time, if any
                    var validPointages = pointages.Where(p => p.DateTimeScan.HasValue && p.DateTimeScan >= startTime && p.DateTimeScan <= endTime).ToList();

                    DateTime? firstPointage = validPointages.Count > 0 ? validPointages.First().DateTimeScan : null;
                    DateTime? lastPointage = validPointages.Count > 0 ? validPointages.Last().DateTimeScan : null;

                    // Create the view model for this slot 
                    tourSlots.Add(new TourDuJourViewModel
                    {
                        TourId = tour.Id,
                        TourRefTour = tour.RefTour,
                        Date = date,
                        NumeroTour = i + 1,
                        debTour = startTime,
                        finTour = endTime,
                        debPointage = firstPointage,
                        finPointage = lastPointage,
                        tourFait = tourComplete,
                        ZonesRequises = zonesTournee.Count,
                        ZonesPointees = zonesPointees.Count,
                        TourAssigné = pointages.Any() && pointages.All(p => p.IsChecked == 0 && p.DateTimeAssign.HasValue),
                        userId = pointages.FirstOrDefault()?.User.UserName,
                        observation = pointages.FirstOrDefault()?.Observation?.Description
                    });
                }

                var users = await (from u in _context.Users
                                   join ur in _context.UserRoles on u.Id equals ur.UserId into userRoles
                                   from ur in userRoles.DefaultIfEmpty()
                                   join r in _context.Roles on ur.RoleId equals r.Id into roles
                                   from r in roles.DefaultIfEmpty()
                                   where (r == null || (r.Name != "admin" && r.Name != "chef")) && u.IsLocked != true
                                   select u.UserName).ToListAsync();

                var observation = await _context.Observations
                                .ToListAsync();

                ViewBag.Tour = tour;
                ViewBag.Users = users;
                ViewBag.Observation = observation;

                // 3. Return the view with the time slots
                return View(tourSlots);
            }
            catch (Exception ex)
            {
                // Better error handling
                TempData["ErrorMessage"] = "Une erreur est survenue: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task<checkTourPointee> checkTourPointee(int? id)
        {
            try
            {
                DateTime? nextTour = default;
                List<int> zonesPointées = new List<int>();
                List<int> zoneAssigné = new List<int>();
                bool tourPointee = false;

                // 🔍 Vérifier si la tournée existe
                var tour = await _context.Tours.FirstOrDefaultAsync(m => m.Id == id);
                if (tour == null)
                {
                    return new checkTourPointee { success = false, message = "Tournée introuvable.", tourPointee = false };
                }

                // 🔍 Récupérer le temps debut de la tournée en cours
                DateTime? lastTour = await TourActuelleAsync(id);

                if (lastTour == null)
                {
                    return new checkTourPointee
                    {
                        success = false,
                        message = $"Exception lastTour pour {tour.RefTour}.",
                        tourPointee = false,

                    };
                }

                if (lastTour == DateTime.MinValue)
                {
                    return new checkTourPointee
                    {
                        success = true,
                        message = $"Aucune tournée en cours pour {tour.RefTour}. Veuillez attendre jusqu'au {tour.DebTour}",
                        tourPointee = false,
                        currentTourTime = tour.DebTour.ToString("HH:mm"),
                    };
                }

                // 🔍 Récupérer les zones associées à cette tournée
                var zonesTournee = await _context.PlanTours
                    .Where(pt => pt.TourId == id)
                    .Select(pt => pt.ZoneId)
                    .ToListAsync();

                if (!zonesTournee.Any())
                {
                    return new checkTourPointee { success = false, message = "Aucune zone définie pour cette tournée.", tourPointee = false };
                }

                // 🔥 Si aucune fréquence n'est définie (tour unique)
                if (tour.FrqTourMin == 0 || tour.FrqTourMin == null)
                {
                    // 🔍 Vérifier si la tournée actuelle a été entièrement pointée
                    zonesPointées = await _context.Pointages
                        .Where(p => p.PlanTour.TourId == id &&
                                    zonesTournee.Contains(p.PlanTour.ZoneId) &&
                                    p.DateTimeScan != null &&
                                    p.DateTimeScan >= lastTour.Value &&
                                    //p.DateTimeScan.Value.Day == lastTour.Value.Day &&
                                    p.IsChecked == 1)
                        .Select(p => p.PlanTour.ZoneId)
                        .Distinct()
                        .ToListAsync();

                    zoneAssigné = await _context.Pointages
                        .Where(p => p.PlanTour.TourId == id &&
                                    zonesTournee.Contains(p.PlanTour.ZoneId) &&
                                    p.DateTimeDebTour >= lastTour.Value &&
                                    p.DateTimeDebTour.Value.Day == lastTour.Value.Day &&
                                    (p.IsChecked == 0 || p.DateTimeAssign != null))
                        .Select(p => p.PlanTour.ZoneId)
                        .Distinct()
                        .ToListAsync();

                    tourPointee = zonesTournee.All(zoneId => zonesPointées.Contains(zoneId));

                    return new checkTourPointee
                    {
                        success = true,
                        message = tourPointee ? "Toutes les zones ont été pointées, pas de tournée restante aujourd'hui."
                                              : "Certaines zones n'ont pas été pointées, la tournée est incomplète.",
                        tourPointee = tourPointee,
                        zonePointée = zonesPointées,
                        currentTourTime = tour.DebTour.ToString("HH:mm"),
                        nextTourTime = "Tournée Faite",
                        tourAssigné = zoneAssigné.Count > 0 ? true : false
                    };
                }


                // 🔍 Calcul du prochain passage
                nextTour = lastTour.Value.AddMinutes(tour.FrqTourMin.Value);

                // 🔍 Vérifier si la tournée actuelle a été entièrement pointée
                zonesPointées = await _context.Pointages
                    .Where(p => p.PlanTour.TourId == id &&
                                zonesTournee.Contains(p.PlanTour.ZoneId) &&
                                p.DateTimeScan != null &&
                                p.DateTimeScan >= lastTour.Value &&
                                p.DateTimeScan <= nextTour &&
                                p.IsChecked == 1)
                    .Select(p => p.PlanTour.ZoneId)
                    .Distinct()
                    .ToListAsync();
                if (zonesPointées.Count() == 0)
                {
                    zoneAssigné = await _context.Pointages
                        .Where(p => p.PlanTour.TourId == id &&
                                    zonesTournee.Contains(p.PlanTour.ZoneId) &&
                                    p.DateTimeDebTour >= lastTour.Value &&
                                    p.DateTimeFinTour <= nextTour &&
                                    (p.IsChecked == 0 || p.DateTimeAssign != null))
                        .Select(p => p.PlanTour.ZoneId)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    zoneAssigné = await _context.Pointages
                        .Where(p => p.PlanTour.TourId == id &&
                                    zonesTournee.Contains(p.PlanTour.ZoneId) &&
                                    p.DateTimeDebTour >= nextTour &&
                                    p.DateTimeFinTour <= nextTour.Value.AddMinutes(tour.FrqTourMin.Value) &&
                                    (p.IsChecked == 0 || p.DateTimeAssign != null))
                        .Select(p => p.PlanTour.ZoneId)
                        .Distinct()
                        .ToListAsync();
                }

                    tourPointee = zonesTournee.All(zoneId => zonesPointées.Contains(zoneId));

                // 🔥 Gestion du passage après minuit
                if (tour.FinTour.HasValue)
                {
                    DateTime finTourTime = now.Date.AddHours(tour.FinTour.Value.Hour).AddMinutes(tour.FinTour.Value.Minute);
                    if (tour.FinTour.Value.Hour < tour.DebTour.Hour)
                    {
                        finTourTime = finTourTime.AddDays(1); // Passage au lendemain
                    }

                    // 🚫 Si la prochaine tournée dépasse l'heure de fin, arrêter
                    if (nextTour > finTourTime)
                    {
                        return new checkTourPointee
                        {
                            success = false,
                            message = tourPointee ? "Toutes les tournées sont terminées pour aujourd'hui."
                                                  : "La dernière tournée n'a pas été complétée avant la fin prévue.",
                            tourPointee = tourPointee,
                            currentTourTime = tour.DebTour.ToString("HH:mm"),
                            nextTourTime = "Tournée Faite",
                        };
                    }
                }

                // ⏳ Calcul du temps restant avant la prochaine tournée
                TimeSpan diff = nextTour.Value - now;


                int diffHours = (int)diff.TotalHours;
                int diffMinutes = diff.Minutes;

                string formattedHours = diffHours.ToString().PadLeft(2, '0');
                string formattedMinutes = diffMinutes.ToString().PadLeft(2, '0');

                return new checkTourPointee
                {
                    success = !tourPointee,
                    message = tourPointee ? "Tournée complétée, prochaine tournée à venir."
                                          : "Certaines zones n'ont pas été pointées, en attente de validation.",
                    currentTourTime = lastTour.Value.ToString("HH:mm"),
                    nextTourTime = nextTour.Value.ToString("HH:mm"),
                    countdown = $"{formattedHours}:{formattedMinutes}",
                    tourPointee = tourPointee,
                    zonePointée = zonesPointées,
                    tourAssigné = zoneAssigné.Count > 0 ? true : false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la vérification du pointage pour la tournée ID {Id}", id);
                return new checkTourPointee { success = false, message = "Une erreur est survenue. Veuillez réessayer.", tourPointee = false };
            }
        }

        public async Task<IActionResult> Check(int? id)
        {
            {
                var result = await checkTourPointee(id);
                return Json(result);
            }
        }

        //private async Task<checkTourPointee> checkTourPointee(int? id)
        //{
        //    try
        //    {
        //        // 🔍 Récupération du rôle utilisateur
        //        var role = User.FindFirstValue(ClaimTypes.Role);
        //        bool isAdminOrChef = role == "admin" || role == "chef";

        //        // 🔍 Validation et récupération de la tournée
        //        var tour = await _context.Tours.FirstOrDefaultAsync(m => m.Id == id);
        //        if (tour == null)
        //        {
        //            return CreateErrorResponse("Tournée introuvable.");
        //        }

        //        // 🔍 Récupération du temps de début de la tournée en cours
        //        DateTime? lastTour = await TourActuelleAsync(id);

        //        if (lastTour == null)
        //        {
        //            return CreateErrorResponse($"Exception lastTour pour {tour.RefTour}.");
        //        }

        //        if (lastTour == DateTime.MinValue)
        //        {
        //            return new checkTourPointee
        //            {
        //                success = true,
        //                message = $"Aucune tournée en cours pour {tour.RefTour}. Veuillez attendre jusqu'au {tour.DebTour}",
        //                tourPointee = false,
        //                currentTourTime = tour.DebTour.ToString("HH:mm")
        //            };
        //        }

        //        // 🔍 Récupération des zones de la tournée
        //        var zonesTournee = await _context.PlanTours
        //            .Where(pt => pt.TourId == id)
        //            .Select(pt => pt.ZoneId)
        //            .ToListAsync();

        //        if (!zonesTournee.Any())
        //        {
        //            return CreateErrorResponse("Aucune zone définie pour cette tournée.");
        //        }

        //        // 🔥 Gestion des tournées uniques (sans fréquence)
        //        if (tour.FrqTourMin <= 0 || tour.FrqTourMin == null )
        //        {
        //            return await HandleSingleTourAsync(id, zonesTournee, lastTour.Value, tour, isAdminOrChef);
        //        }

        //        // 🔍 Gestion des tournées récurrentes
        //        return await HandleRecurringTourAsync(id, zonesTournee, lastTour.Value, tour, isAdminOrChef);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur lors de la vérification du pointage pour la tournée ID {Id}", id);
        //        return CreateErrorResponse("Une erreur est survenue. Veuillez réessayer.");
        //    }
        //}

        //private async Task<checkTourPointee> HandleSingleTourAsync(int? id, List<int> zonesTournee, DateTime lastTour, Tour tour, bool isAdminOrChef)
        //{
        //    var today = now.Date;
        //    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    // Récupération optimisée des pointages pour tournée unique
        //    var pointages = await _context.Pointages
        //        .Where(p => p.PlanTour.TourId == id &&
        //                   zonesTournee.Contains(p.PlanTour.ZoneId) &&
        //                   ((p.DateTimeScan.HasValue &&
        //                     p.DateTimeScan >= lastTour &&
        //                     p.DateTimeScan.Value.Date == today) ||
        //                    (p.DateTimeDebTour.HasValue &&
        //                     p.DateTimeDebTour >= lastTour &&
        //                     p.DateTimeDebTour.Value.Date == today)))
        //        .Select(p => new {
        //            ZoneId = p.PlanTour.ZoneId,
        //            UserId = p.UserId,
        //            IsScanned = p.DateTimeScan.HasValue && p.IsChecked == 1 &&
        //                       p.DateTimeScan >= lastTour && p.DateTimeScan.Value.Date == today,
        //            IsAssigned = p.DateTimeDebTour.HasValue && p.IsChecked == 0 &&
        //                        p.DateTimeDebTour >= lastTour && p.DateTimeDebTour.Value.Date == today
        //        })
        //        .ToListAsync();

        //    var zonesPointees = pointages.Where(p => p.IsScanned).Select(p => p.ZoneId).Distinct().ToList();
        //    var hasAssignedZones = pointages.Any(p => p.UserId == currentUserId && p.IsAssigned);
        //    var tourPointee = zonesTournee.All(zoneId => zonesPointees.Contains(zoneId));

        //    return new checkTourPointee
        //    {
        //        success = true,
        //        message = tourPointee
        //            ? "Toutes les zones ont été pointées, pas de tournée restante aujourd'hui."
        //            : "Certaines zones n'ont pas été pointées, la tournée est incomplète.",
        //        tourPointee = tourPointee,
        //        zonePointée = zonesPointees,
        //        currentTourTime = tour.DebTour.ToString("HH:mm"),
        //        nextTourTime = "Tournée Faite",
        //        tourAssigné = isAdminOrChef ? true : hasAssignedZones
        //    };
        //}

        //private async Task<checkTourPointee> HandleRecurringTourAsync(int? id, List<int> zonesTournee, DateTime lastTour, Tour tour, bool isAdminOrChef)
        //{
        //    var nextTour = lastTour.AddMinutes(tour.FrqTourMin.Value);

        //    // 🔥 Vérification si la prochaine tournée dépasse l'heure de fin
        //    if (tour.FinTour.HasValue && IsNextTourAfterEndTime(tour, nextTour))
        //    {
        //        var (tourComplete, zonesPointees, hasAssigned) = await GetTourStatusAsync(id, zonesTournee, lastTour, nextTour, isAdminOrChef);

        //        return new checkTourPointee
        //        {
        //            success = false,
        //            message = tourComplete
        //                ? "Toutes les tournées sont terminées pour aujourd'hui."
        //                : "La dernière tournée n'a pas été complétée avant la fin prévue.",
        //            tourPointee = tourComplete,
        //            currentTourTime = tour.DebTour.ToString("HH:mm"),
        //            nextTourTime = "Tournée Faite",
        //            tourAssigné = isAdminOrChef ? true : hasAssigned // true pour admin/chef, logique normale pour autres
        //        };
        //    }

        //    // 🔍 Récupération du statut de la tournée actuelle
        //    var (isComplete, pointedZones, hasAssignedZones) = await GetTourStatusAsync(id, zonesTournee, lastTour, nextTour, isAdminOrChef);

        //    // ⏳ Calcul du temps restant
        //    var countdown = CalculateCountdown(nextTour);

        //    return new checkTourPointee
        //    {
        //        success = !isComplete,
        //        message = isComplete
        //            ? "Tournée complétée, prochaine tournée à venir."
        //            : "Certaines zones n'ont pas été pointées, en attente de validation.",
        //        currentTourTime = lastTour.ToString("HH:mm"),
        //        nextTourTime = nextTour.ToString("HH:mm"),
        //        countdown = countdown,
        //        tourPointee = isComplete,
        //        zonePointée = pointedZones,
        //        tourAssigné = isAdminOrChef ? true : hasAssignedZones // true pour admin/chef, logique normale pour autres
        //    };
        //}

        //private async Task<(bool IsComplete, List<int> ZonesPointees, bool HasAssigned)> GetTourStatusAsync(
        //    int? id, List<int> zonesTournee, DateTime startTime, DateTime endTime, bool isAdminOrChef)
        //{
        //    // 🔐 Pour les admin/chef, récupération simplifiée (pas besoin de vérifier les assignations)
        //    if (isAdminOrChef)
        //    {
        //        var zonesPointeesAdmin = await _context.Pointages
        //            .Where(p => p.PlanTour.TourId == id &&
        //                       zonesTournee.Contains(p.PlanTour.ZoneId) &&
        //                       p.DateTimeScan.HasValue &&
        //                       p.DateTimeScan >= startTime &&
        //                       p.DateTimeScan <= endTime &&
        //                       p.IsChecked == 1)
        //            .Select(p => p.PlanTour.ZoneId)
        //            .Distinct()
        //            .ToListAsync();

        //        var isComplete = zonesTournee.All(zoneId => zonesPointeesAdmin.Contains(zoneId));
        //        return (isComplete, zonesPointeesAdmin, true); // Toujours assigné pour admin/chef
        //    }

        //    // Récupération de l'ID utilisateur actuel
        //    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    // Logique complète pour les utilisateurs standards (vérifier les assignations réelles)
        //    var pointages = await _context.Pointages
        //        .Where(p => p.PlanTour.TourId == id &&
        //                   zonesTournee.Contains(p.PlanTour.ZoneId) &&
        //                   ((p.DateTimeScan.HasValue &&
        //                     p.DateTimeScan >= startTime &&
        //                     p.DateTimeScan <= endTime) ||
        //                    (p.DateTimeDebTour.HasValue &&
        //                     p.DateTimeFinTour.HasValue &&
        //                     p.DateTimeDebTour >= startTime &&
        //                     p.DateTimeFinTour <= endTime)))
        //        .Select(p => new {
        //            ZoneId = p.PlanTour.ZoneId,
        //            UserId = p.UserId,
        //            IsScanned = p.DateTimeScan.HasValue && p.IsChecked == 1 &&
        //                       p.DateTimeScan >= startTime && p.DateTimeScan <= endTime,
        //            IsAssigned = (p.IsChecked == 0 || p.DateTimeAssign.HasValue) 
        //                        && p.DateTimeDebTour >= startTime && p.DateTimeFinTour <= endTime
        //        })
        //        .ToListAsync();

        //    var zonesPointees = pointages.Where(p => p.IsScanned).Select(p => p.ZoneId).Distinct().ToList();

        //    // Vérifier si l'utilisateur actuel est assigné à cette tournée
        //    var isCurrentUserAssigned = pointages.Any(p => p.UserId == currentUserId && p.IsAssigned);

        //    var isCompleteUser = zonesTournee.All(zoneId => zonesPointees.Contains(zoneId));

        //    return (isCompleteUser, zonesPointees, isCurrentUserAssigned);
        //}

        //private static bool IsNextTourAfterEndTime(Tour tour, DateTime nextTour)
        //{
        //    if (!tour.FinTour.HasValue) return false;

        //    var today = now.Date;
        //    var finTourTime = today.Add(tour.FinTour.Value.ToTimeSpan());

        //    // Gestion du passage après minuit
        //    if (tour.FinTour.Value.Hour < tour.DebTour.Hour)
        //    {
        //        finTourTime = finTourTime.AddDays(1);
        //    }

        //    return nextTour > finTourTime;
        //}

        //private static string CalculateCountdown(DateTime nextTour)
        //{
        //    var diff = nextTour - now;
        //    var hours = Math.Max(0, (int)diff.TotalHours);
        //    var minutes = Math.Max(0, diff.Minutes);

        //    return $"{hours:D2}:{minutes:D2}";
        //}

        //private static checkTourPointee CreateErrorResponse(string message)
        //{
        //    return new checkTourPointee
        //    {
        //        success = false,
        //        message = message,
        //        tourPointee = false
        //    };
        //}

        //public async Task<IActionResult> Check(int? id)
        //{
        //    var result = await checkTourPointee(id);
        //    return Json(result);
        //}

        //public async Task<DateTime?> TourAssignéAsync(int? id)
        //{
        //    try
        //    {
        //        // 🔍 Vérifier si la tournée existe
        //        var tour = await _context.Tours.FirstOrDefaultAsync(m => m.Id == id);
        //        if (tour == null)
        //        {
        //            return null;
        //        }

        //        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //        if (string.IsNullOrEmpty(userId))
        //        {
        //            return null;
        //        }
        //        DateTime defaultTime = DateTime.MinValue;

        //        var PointagesAssigné = await _context.Pointages
        //            .Include(pa => pa.PlanTour)
        //            .Where(pa => pa.PlanTour.TourId == id && pa.UserId == userId
        //                && pa.IsChecked == 0
        //                && now >= pa.DateTimeDebTour
        //                && now <= pa.DateTimeFinTour)
        //            .FirstOrDefaultAsync();

        //        DateTime now = PointagesAssigné != null && PointagesAssigné.DateTimeScan.HasValue
        //            ? PointagesAssigné.DateTimeScan.Value
        //            : now;

        //        DateTime startTour = now.Date.AddHours(tour.DebTour.Hour).AddMinutes(tour.DebTour.Minute);

        //        if (now < startTour)
        //        {
        //            return defaultTime;
        //        }
        //        DateTime lastTour = startTour;

        //        if (!tour.FinTour.HasValue)
        //        {
        //            return lastTour;
        //        }

        //        DateTime endTour = now.Date.AddHours(tour.FinTour.Value.Hour).AddMinutes(tour.FinTour.Value.Minute);

        //        if (tour.FinTour.Value.Hour < tour.DebTour.Hour)
        //        {
        //            endTour = endTour.AddDays(1);
        //        }

        //        if (tour.FrqTourMin.HasValue)
        //        {
        //            int frequency = tour.FrqTourMin.Value;

        //            while (lastTour.AddMinutes(frequency) <= now && lastTour.AddMinutes(frequency) <= endTour)
        //            {
        //                lastTour = lastTour.AddMinutes(frequency);
        //            }
        //        }

        //        if (lastTour > endTour)
        //        {
        //            return defaultTime;
        //        }

        //        return lastTour;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Erreur lors du calcul de la tournée actuelle pour ID {Id}", id);
        //        return null;
        //    }
        //}



        public async Task<DateTime?> TourActuelleAsync(int? id)
        {
            try
            {
                // 🔍 Vérifier si la tournée existe
                var tour = await _context.Tours.FirstOrDefaultAsync(m => m.Id == id);
                if (tour == null)
                {
                    return null;
                }
                DateTime defaultTime = DateTime.MinValue;
                DateTime startTour = defaultTime;
                DateTime endTour = defaultTime;
                if (now.Hour < 9)
                {
                    startTour = now.Date.AddDays(-1).AddHours(tour.DebTour.Hour).AddMinutes(tour.DebTour.Minute);
                }
                else
                {
                    startTour = now.Date.AddHours(tour.DebTour.Hour).AddMinutes(tour.DebTour.Minute);
                }
                /*
                if (now < startTour)
                {
                    return defaultTime;
                }*/

                DateTime lastTour = startTour;

                if (!tour.FinTour.HasValue)
                {
                    return lastTour;
                }

                

                if (now.Hour < 9)
                {
                    endTour = now.Date.AddDays(-1).AddHours(tour.FinTour.Value.Hour).AddMinutes(tour.FinTour.Value.Minute);
                }
                else
                {
                    endTour = now.Date.AddHours(tour.FinTour.Value.Hour).AddMinutes(tour.FinTour.Value.Minute);
                }

                if (tour.FinTour.Value.Hour < tour.DebTour.Hour)
                {
                    endTour = endTour.AddDays(1);
                }

                if (tour.FrqTourMin.HasValue)
                {
                    int frequency = tour.FrqTourMin.Value;

                    while (lastTour.AddMinutes(frequency) <= now && lastTour.AddMinutes(frequency) <= endTour)
                    {
                        lastTour = lastTour.AddMinutes(frequency);
                    }
                }

                if (lastTour > endTour)
                {
                    return defaultTime;
                }

                return lastTour;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du calcul de la tournée actuelle pour ID {Id}", id);
                return null;
            }
        }

        [HttpGet]
        [Route("Agent/ScannerQRCode")]
        public IActionResult ScannerQRCode(int? id)
        {
            // Supprimer les en-têtes anti-cache par défaut
            Response.Headers.Remove("Cache-Control");
            Response.Headers.Remove("Pragma");
            Response.Headers.Remove("Expires");

            // Ajouter un cache privé de 60 secondes
            Response.Headers["Cache-Control"] = "private, max-age=60";
            return View("qrCodePage");
        }

        [HttpPost]
        public async Task<IActionResult> ValiderQRCode([FromBody] ScanQRCodeDTO data)
        {
            try
            {

                var planTour = await _context.PlanTours

                        .Include(pt => pt.Tour)
                        .Include(pt => pt.Zone)
                        .FirstOrDefaultAsync(m => m.Id == data.PlanTourId);

                if (planTour == null || planTour.Zone == null)
                {
                    return Json(new { message = "Erreur PlanTournée ", redirectUrl = Url.Action("Index") });
                }

                if (string.IsNullOrEmpty(data.qrCodeText))
                {
                    return Json(new { message = "QR Code invalide.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                }

                string? qrCodeTag = null;
                int qrCodeZone = -1;
                string[]? qrCodeParts = data.qrCodeText.Split(";");
                
                if (qrCodeParts.Length >= 2)
                {
                    if (int.TryParse(qrCodeParts[0], out qrCodeZone))
                    {
                        qrCodeTag = qrCodeParts[1];
                    }
                }

                if (planTour.ZoneId != qrCodeZone)
                {
                    return Json(new { message = "QR Code ne correspond pas à la zone attendue.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                }
                // Charger l'utilisateur avec une seule requête
                string? username = User.Identity?.Name;
                var user = await _context.Users.Where(u => u.IsLocked != true && u.UserName == username).FirstOrDefaultAsync();

                if (user == null || string.IsNullOrEmpty(username))
                {
                    return Json(new { message = "Problème d'utilisateur", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                }

                var pointagesIncomplets = await _context.Pointages
                .Where(p => p.Id == data.PointageId
                        && p.IsChecked == 0
                        && p.DateTimeAssign != null
                        ).FirstOrDefaultAsync();

                if (pointagesIncomplets == null)
                {
                    return Json(new { message = "Pointage non trouvé", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });

                    /*
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var pointage = new PointageAgent
                            {
                                UserId = user.Id,
                                User = user,
                                PlanTourId = planTour.Id,
                                PlanTour = planTour,
                                IsChecked = 1,
                                DateTimeScan = data.datetimescan.HasValue ? data.datetimescan.Value.AddHours(1) : now,
                                DateTimeAssign = null,
                                IsValid = (qrCodeTag != null && qrCodeTag == planTour.Zone.Tag) ? 1 : 0
                            };

                            _context.Pointages.Add(pointage);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            // Vérifier l'état de la tournée après le pointage
                            var result = await checkTourPointee(planTour.TourId);

                            if (result.tourPointee)
                            {
                                return Json(new { message = "Tournée pointée !", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                            }
                            else if (!result.tourPointee && result.success)
                            {
                                return Json(new { message = "Pointage réussi ! Continuez la tournée.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                            }
                            return Json(new { message = "Erreur de scan, veuillez réessayer.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Erreur lors de l'enregistrement du pointage pour la tournée ID {TourId} et zone ID {ZoneId}", planTour.TourId, planTour.ZoneId);
                            return Json(new { message = "Une erreur est survenue lors du pointage. Veuillez réessayer.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                        }
                    }
*/
                }
                else
                {

                    // Sauvegarder le pointage dans une transaction pour garantir l'intégrité
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            pointagesIncomplets.IsChecked = 1;
                            pointagesIncomplets.IsValid = (qrCodeTag != null && qrCodeTag == planTour.Zone.Tag) ? 1 : 0;
                            pointagesIncomplets.DateTimeScan = data.datetimescan.HasValue ? data.datetimescan.Value.AddHours(1) : now;
                            pointagesIncomplets.ObservationId = null;
                            pointagesIncomplets.Last_Update = now;

                            _context.Update(pointagesIncomplets);
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            var result = await checkTourPointee(planTour.TourId);

                            if (result.tourPointee)
                            {
                                return Json(new { message = "Tournée pointée !", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                            }
                            else if (!result.tourPointee && result.success)
                            {
                                return Json(new { message = "Pointage réussi ! Continuez la tournée.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                            }
                            return Json(new { message = "Erreur de scan, veuillez réessayer.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "Erreur lors de l'enregistrement du pointage pour la tournée ID {TourId} et zone ID {ZoneId}", planTour.TourId, planTour.ZoneId);
                            return Json(new { message = "Une erreur est survenue lors du pointage. Veuillez réessayer.", redirectUrl = Url.Action("DebutTour", new { id = planTour.TourId }) });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur générale dans ValiderQRCode pour la tournée ");
                return Json(new { message = "Une erreur inattendue est survenue. Veuillez réessayer.", redirectUrl = Url.Action("Index") });
            }
        }
    }
}