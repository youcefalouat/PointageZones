using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PointageZones.Data;
using PointageZones.Models;
using QRCoder;


namespace PointageZones.Controllers
{
    [Authorize(Roles = "admin")]
    public class ZonesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ZonesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Zones
        public async Task<IActionResult> Index()
        {
            return View(await _context.Zones.ToListAsync());
        }

        // GET: Zones/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var zone = await _context.Zones
                .FirstOrDefaultAsync(m => m.Id == id);
            if (zone == null)
            {
                return NotFound();
            }

            return View(zone);
        }

        // GET: Zones/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Zones/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,RefZone,Type")] Zone zone)
        {
            if (ModelState.IsValid)
            {
                zone.lastUpdate = DateTime.Now;
                _context.Add(zone);
                await _context.SaveChangesAsync();
                TempData["Notification"] = "Zone Créé avec succés";
                return RedirectToAction(nameof(Index));
            }
            return View(zone);
        }

        // GET: Zones/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var zone = await _context.Zones.FindAsync(id);
            if (zone == null)
            {
                return NotFound();
            }
            return View(zone);
        }

        // POST: Zones/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,RefZone,Type")] Zone zone)
        {
            if (id != zone.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    zone.lastUpdate = DateTime.Now;
                    _context.Update(zone);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ZoneExists(zone.Id))
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
            return View(zone);
        }

        // GET: Zones/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var zone = await _context.Zones
                .FirstOrDefaultAsync(m => m.Id == id);
            if (zone == null)
            {
                return NotFound();
            }

            return View(zone);
        }

        // POST: Zones/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var zone = await _context.Zones.FindAsync(id);
            if (zone != null)
            {
                _context.Zones.Remove(zone);
            }

            await _context.SaveChangesAsync();
            TempData["Notification"] = "Zone Supprimé avec succés";
            return RedirectToAction(nameof(Index));
        }

        private bool ZoneExists(int id)
        {
            return _context.Zones.Any(e => e.Id == id);
        }

        

        public async Task<IActionResult> DownloadQRCodeAsync(int? id)
        {
            
            if (id == null)
            {
                return NotFound();
            }
            var zone = await _context.Zones
                .FirstOrDefaultAsync(m => m.Id == id);

            if (zone != null)
            {

                try
                {
                    Random random = new Random();
                    string newTag;

                    do
                    {
                        // Generate a random string of 10 characters
                        char[] tagArray = new char[10];

                        for (int i = 0; i < 10; i++)
                        {
                            char randomChar;
                            do
                            {
                                // Generate a random character from the printable ASCII range (32-126)
                                randomChar = (char)random.Next(32, 127);

                                // Skip semicolons
                            } while (randomChar == ';');

                            tagArray[i] = randomChar;
                        }

                        newTag = new string(tagArray);

                        // Check if this tag already exists in the database
                    } while (await _context.Zones.AnyAsync(z => z.Tag == newTag));

                    zone.Tag = newTag;
                    zone.lastUpdate = DateTime.Now; 
                    _context.Update(zone);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ZoneExists(zone.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                
            }


            string zoneRef = zone.RefZone;
            string zoneQR = zone.Id + ";" + zone.Tag;

            if (string.IsNullOrEmpty(zoneRef))
            {
                return BadRequest("Invalid zone reference");
            }

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(zoneQR, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
            {
                // Define text properties
                Font font = new Font("Arial", 20, FontStyle.Bold);
                int textPadding = 10;

                // Calculate the new image size
                int width = qrCodeImage.Width;
                int height = qrCodeImage.Height + textPadding + 40; // Extra space for text

                using (Bitmap finalImage = new Bitmap(width, height))
                using (Graphics graphics = Graphics.FromImage(finalImage))
                using (MemoryStream ms = new MemoryStream())
                {
                    graphics.Clear(Color.White);

                    // Draw the QR code
                    graphics.DrawImage(qrCodeImage, new Point(0, 0));

                    // Draw the text below the QR code
                    StringFormat stringFormat = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near
                    };

                    Rectangle textRect = new Rectangle(0, qrCodeImage.Height + textPadding, width, 40);
                    graphics.DrawString(zoneRef, font, Brushes.Black, textRect, stringFormat);

                    // Save and return the image
                    finalImage.Save(ms, ImageFormat.Png);
                    return File(ms.ToArray(), "image/png", $"{zoneRef}_QRCode.png");
                }
            }
        }

    }
}
