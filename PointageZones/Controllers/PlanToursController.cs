using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PointageZones.Data;
using PointageZones.Models;

namespace PointageZones.Controllers
{
    [Authorize(Roles = "admin")]
    public class PlanToursController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PlanToursController(ApplicationDbContext context)
        {
            _context = context;
        }

        

        // GET: PlanTours
        //public async Task<IActionResult> Index()
        //{
        //    var planTours = await _context.PlanTours.AsNoTracking()
        //                    .Include(p => p.Tour)
        //                    .Include(p => p.Zone)
        //                    .ToListAsync();
        //    return View(planTours);
        //}
        public async Task<IActionResult> Index()
        {
            var tours = await _context.Tours
                .Include(t => t.PlanTours)
                .ThenInclude(pt => pt.Zone)
                .ToListAsync();

            return View(tours);
        }

        // GET: PlanTours/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var planTour = await _context.PlanTours
                .Include(p => p.Tour)
                .Include(p => p.Zone)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (planTour == null)
            {
                return NotFound();
            }

            return View(planTour);
        }

        // GET: PlanTours/Create
        public IActionResult Create()
        {
            ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour");
            ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone");
            return View();
        }

        // POST: PlanTours/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Id,TourId,ZoneId")] PlanTour planTour)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            _context.Add(planTour);
        //            await _context.SaveChangesAsync();
        //            return RedirectToAction(nameof(Index));
        //        }
        //        catch (Exception ex)
        //        {
        //            ModelState.AddModelError("", "Erreur lors de l'ajout du plan de tournée : " + ex.Message);
        //        }
        //    }
        //    ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour", planTour.TourId);
        //    ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone", planTour.ZoneId);
        //    return View(planTour);
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TourId,ZoneId")] PlanTour planTour)
        {

            // Vérifier si le couple (TourId, ZoneId) existe déjà
            bool existe = await _context.PlanTours
                .AnyAsync(pt => pt.TourId == planTour.TourId && pt.ZoneId == planTour.ZoneId);

            if (existe)
            {
                ModelState.AddModelError("", "Ce plan de tournée existe déjà !");
                ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour", planTour.TourId);
                ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone", planTour.ZoneId);
                return View(planTour);
            }
            else
            {

                // Charger les objets Tour et Zone avant l'enregistrement
                planTour.Tour = await _context.Tours.FindAsync(planTour.TourId);
                planTour.Zone = await _context.Zones.FindAsync(planTour.ZoneId);

                if (planTour.Tour != null && planTour.Zone != null)
                {
                    _context.Add(planTour);
                    await _context.SaveChangesAsync();
                }

                // Recharger les listes pour éviter une erreur sur la vue
                ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour", planTour.TourId);
                ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone", planTour.ZoneId);
                return View(planTour);

            }

        }
        // GET: PlanTours/Edit/5
        //public async Task<IActionResult> Edit(int? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var planTour = await _context.PlanTours.FindAsync(id);
        //    if (planTour == null)
        //    {
        //        return NotFound();
        //    }
        //    ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour", planTour.TourId);
        //    ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone", planTour.ZoneId);
        //    return View(planTour);
        //}
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tour = await _context.Tours
                .Include(t => t.PlanTours)
                .ThenInclude(pt => pt.Zone)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tour == null)
            {
                return NotFound();
            }

            var allZones = await _context.Zones.ToListAsync();
            var selectedZones = tour.PlanTours.Select(pt => pt.ZoneId).ToList();

            ViewBag.Zones = allZones.Select(z => new SelectListItem
            {
                Value = z.Id.ToString(),
                Text = z.RefZone,
                Selected = selectedZones.Contains(z.Id)
            }).ToList();
            return View(tour);
        }

        // POST: PlanTours/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(int id, [Bind("Id,TourId,ZoneId")] PlanTour planTour)
        //{
        //    if (id != planTour.Id)
        //    {
        //        return NotFound();
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            _context.Update(planTour);
        //            await _context.SaveChangesAsync();
        //        }
        //        catch (DbUpdateConcurrencyException)
        //        {
        //            if (!PlanTourExists(planTour.Id))
        //            {
        //                return NotFound();
        //            }
        //            else
        //            {
        //                throw;
        //            }
        //        }
        //        return RedirectToAction(nameof(Index));
        //    }
        //    ViewData["TourId"] = new SelectList(_context.Tours, "Id", "RefTour", planTour.TourId);
        //    ViewData["ZoneId"] = new SelectList(_context.Zones, "Id", "RefZone", planTour.ZoneId);
        //    return View(planTour);
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int[] ZoneIds)
        {
            var tour = await _context.Tours
                .Include(t => t.PlanTours)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tour == null)
            {
                return NotFound();
            }

            // Supprimer les anciennes affectations
            _context.PlanTours.RemoveRange(tour.PlanTours);

            // Ajouter les nouvelles affectations
            foreach (var zoneId in ZoneIds)
            {
                // Charger les objets Tour et Zone avant l'enregistrement
                var _Tour = await _context.Tours.FindAsync(id);
                var _Zone = await _context.Zones.FindAsync(zoneId);

                // Vérifier que les objets ne sont pas null avant de les utiliser
                if (_Tour != null && _Zone != null)
                {
                    _context.PlanTours.Add(new PlanTour { TourId = id, Tour = _Tour, ZoneId = zoneId, Zone = _Zone });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Tours");
        }


        // GET: PlanTours/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var planTour = await _context.PlanTours
                .Include(p => p.Tour)
                .Include(p => p.Zone)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (planTour == null)
            {
                return NotFound();
            }

            return View(planTour);
        }

        // POST: PlanTours/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var planTour = await _context.PlanTours.FindAsync(id);
            if (planTour != null)
            {
                _context.PlanTours.Remove(planTour);
                await _context.SaveChangesAsync();
            }

           
            return RedirectToAction(nameof(Index));
        }

        private bool PlanTourExists(int id)
        {
            return _context.PlanTours.Any(e => e.Id == id);
        }
    }
}
