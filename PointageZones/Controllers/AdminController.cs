using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PointageZones.Data;
using PointageZones.DTO;
using PointageZones.Models;

[Authorize(Roles = "admin")]
public class AdminController : Controller
{
    
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public int nbTourParJour(Tour tour)
    {
        if (tour == null) { return 0; }
        if (tour.FinTour == null || tour.FrqTourMin == null)
            return 1; // Une seule occurrence si pas de FinTour ou FrqTourMin

        int dureeTotale = (tour.FinTour < tour.DebTour)
            ? ((tour.FinTour.Value.Hour * 60 + tour.FinTour.Value.Minute + 1440) - (tour.DebTour.Hour * 60 + tour.DebTour.Minute))
            : ((tour.FinTour.Value.Hour * 60 + tour.FinTour.Value.Minute) - (tour.DebTour.Hour * 60 + tour.DebTour.Minute));

        return dureeTotale / tour.FrqTourMin.Value;


    }

    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string selectedUser, string selectedTour)
    {
        DateTime start = startDate ?? DateTime.UtcNow.AddDays(-1);
        DateTime end = endDate ?? DateTime.UtcNow;

        var pointagesQuery = _context.Pointages
            .Include(p => p.User)
            .Include(p => p.PlanTour)
                .ThenInclude(pt => pt.Zone) 
            .Include(p => p.PlanTour)
                .ThenInclude(pt => pt.Tour) 
            .Where(p => p.DateTimeDebTour >= start && p.DateTimeDebTour <= end);

        if (!string.IsNullOrEmpty(selectedUser))
        {
            pointagesQuery = pointagesQuery.Where(p => p.User.UserName == selectedUser);
        }

        if (!string.IsNullOrEmpty(selectedTour))
        {
            pointagesQuery = pointagesQuery.Where(p => p.PlanTour.Tour.RefTour == selectedTour);
        }

        var pointages = await pointagesQuery.ToListAsync();

        var groupedByUser = pointages
            .Where(p => p.IsChecked == 1 && p.DateTimeScan >= start && p.DateTimeScan <= end)
            .GroupBy(p => new { p.User.UserName, p.PlanTour.TourId , p.DateTimeDebTour})
            .Where(g =>
            {
                var tourId = g.Key.TourId;
                var zonesAttendues = _context.PlanTours
                    .Where(pt => pt.TourId == tourId)
                    .Select(pt => pt.ZoneId)
                    .ToList();

                var zonesPointees = g
                    .Where(p => p.PlanTour != null)
                    .Select(p => p.PlanTour.ZoneId)
                    .Distinct()
                    .ToList();

                return zonesAttendues.All(z => zonesPointees.Contains(z));
            })
            .GroupBy(g => g.Key.UserName)
            .Select(g => new UtilisateurPointageDto
            {
                UtilisateurId = g.Key,
                Nombre = g.Count() // nombre de tournées complètes effectuées
            })
            .ToList();
        /*.Where(p => p.PlanTour)
        .GroupBy(p => p.User.UserName ?? string.Empty)
        .Select(g => new UtilisateurPointageDto{UtilisateurId = g.Key,Nombre = g.Count()})
        .ToList();*/

        var yesterday = DateTime.Today.AddDays(-1);
        var tours = _context.Tours
                    .Where(t => t.PlanTours.Count > 0 )
                    .ToList();
        var tourDuJourViewModels = new List<TourDuJourViewModel>();

        foreach (var tour in tours)
        {
            // For each day in the date range
            for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                int totalSlots = nbTourParJour(tour);
                for (int i = 0; i < totalSlots; i++)
                {
                    // Calculate start and end times for this slot
                    var startTime = date.AddHours(tour.DebTour.Hour).AddMinutes(tour.DebTour.Minute + i * (tour.FrqTourMin ?? 0));
                    var endTime = (tour.FinTour == null || tour.FrqTourMin == null) ? startTime.AddMinutes(1440) : startTime.AddMinutes(tour.FrqTourMin.Value);

                    // Get the zones that should be visited during this tour
                    var zonesTournee = await _context.PlanTours
                        .Where(pt => pt.TourId == tour.Id)
                        .Select(pt => pt.ZoneId)
                        .ToListAsync();

                    var point = await _context.Pointages
                        .Where(p => p.PlanTour.TourId == tour.Id && p.DateTimeScan >= startTime && p.DateTimeScan < endTime && p.IsChecked == 1)
                        .OrderBy(p => p.DateTimeScan)
                        .ToListAsync();

                    var zonesPointees = point
                        .Where(p => p.IsChecked == 1 && p.PlanTour != null)
                        .Select(p => p.PlanTour.ZoneId)
                        .Distinct()
                        .ToList();


                    // Check if all required zones were scanned during this time slot
                    bool tourComplete = zonesTournee.Count > 0 &&
                                       zonesTournee.All(z => zonesPointees.Contains(z));

                    // First and last pointage time, if any
                    DateTime? firstPointage = point.Any() ? point.First().DateTimeScan : null;
                    DateTime? lastPointage = point.Any() ? point.Last().DateTimeScan : null;

                    // Create the view model for this slot
                    tourDuJourViewModels.Add(new TourDuJourViewModel
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
                        TourAssigné = pointages.Any() && pointages.All(p => p.IsChecked == 0)
                    });
                }
            }
        }
        /*var groupedByPlan = tours.Select(t => new PlanTourneeRatioDto
        {
            PlanTourneeId = t.RefTour,

            Total = nbTourParJour(t),

            Effectue = Enumerable.Range(0, nbTourParJour(t)).Count(i =>
            {
                var startTime = start.Date.AddHours(t.DebTour.Hour).AddMinutes(t.DebTour.Minute + i * (t.FrqTourMin ?? 0));
                var endTime = (t.FinTour == null || t.FrqTourMin == null) ? startTime.AddMinutes(1440) : startTime.AddMinutes(t.FrqTourMin.Value);

                var zonesTournee = _context.PlanTours.Where(pt => pt.TourId == t.Id).Select(pt => pt.ZoneId).ToList();
                var zonesPointees = _context.Pointages
                    .Where(p => p.TourId == t.Id && p.DateTimeScan >= startTime && p.DateTimeScan < endTime)
                    .Select(p => p.ZoneId)
                    .Distinct()
                    .ToList();

                return zonesTournee.All(zone => zonesPointees.Contains(zone));
            })
        }).ToList();*/

        // Calculate tourDuJourViewModels first
        // Then derive groupedByPlan from it:
        var groupedByPlan = tourDuJourViewModels
            .GroupBy(t => t.TourId ) 
            .Select(g => new PlanTourneeRatioDto
            {
                PlanTourneeId = g.Key,
                refTour = g.First().TourRefTour,
                Total = g.Count(),
                Effectue = g.Count(t => t.tourFait)
            })
            .ToList();

        var viewModel = new AdminDashboardViewModel
        {
            Pointages = pointages,
            NombrePointagesParUtilisateur = groupedByUser,
            RatioTournéesParPlan = groupedByPlan,
            tourDuJourViewModels = tourDuJourViewModels,
            DateDebut = start,
            DateFin = end,
            SelectedUser = selectedUser,
            SelectedTour = selectedTour
        };

        return View(viewModel);
    }
}

