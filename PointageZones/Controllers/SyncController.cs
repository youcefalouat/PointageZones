using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PointageZones.Data;
using PointageZones.Models;

[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SyncController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> SyncScans([FromBody] List<PointageAgent> scans)
    {
        if (scans == null || !scans.Any()) return BadRequest("No data to sync");

        foreach (var scan in scans)
        {
            var existing = await _context.Pointages.FindAsync(scan.Id);
            if (existing == null)
            {
                _context.Pointages.Add(scan);
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Sync successful" });
    }
}
