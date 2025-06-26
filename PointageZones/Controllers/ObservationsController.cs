using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PointageZones.Data;
using PointageZones.Models;

namespace PointageZones.Controllers
{
    public class ObservationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ObservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ObservationsController
        public async Task<ActionResult> IndexAsync()
        {
            return View(await _context.Observations.ToListAsync());
        }

        // GET: ObservationsController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ObservationsController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind("Id,Description")] Observation observation)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    observation.Description = observation.Description.Trim();
                    _context.Add(observation);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                return View(observation); // Ensure a return value for invalid ModelState
            }
            catch
            {
                return View(observation); // Ensure a return value in case of exception
            }
        }

        // GET: Observations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var Observation = await _context.Observations.FindAsync(id);
            if (Observation == null)
            {
                return NotFound();
            }
            return View(Observation);
        }

        // POST: observations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Description")] Observation observation)
        {
            if (id != observation.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(observation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ObservationExists(observation.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                TempData["Notification"] = "Zone Mis a jour avec succés";
                return RedirectToAction(nameof(Index));
            }
            return View(observation);
        }

        // GET: observations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var observation = await _context.Observations
                .FirstOrDefaultAsync(m => m.Id == id);
            if (observation == null)
            {
                return NotFound();
            }

            return View(observation);
        }

        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var observation = await _context.Observations.FindAsync(id);
            if (observation != null)
            {
                _context.Observations.Remove(observation); // Corrected DbSet to Observations
            }

            await _context.SaveChangesAsync();
            TempData["Notification"] = "Observation supprimée avec succès";
            return RedirectToAction(nameof(Index));
        }

        private bool ObservationExists(int id)
        {
            return _context.Observations.Any(e => e.Id == id);
        }
    }
}
