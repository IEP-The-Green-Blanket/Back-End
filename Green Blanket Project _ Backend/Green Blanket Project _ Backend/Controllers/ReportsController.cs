using GB.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using GB.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Green_Blanket_Project___Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly GreenBlanketDbContext _context;

    public ReportsController(GreenBlanketDbContext context)
    {
        _context = context;
    }

    // GET: api/reports
    // This fetches all reports for your new React Dashboard table!
    [HttpGet]
    public async Task<IActionResult> GetReports()
    {
        try
        {
            var reports = await _context.ForumReports
                .OrderByDescending(r => r.Date) // Show newest first
                .Select(r => new
                {
                    id = r.ReportForumId,
                    name = r.Name,
                    email = r.Email,
                    location = r.Location,
                    message = r.Message,
                    dateSubmitted = r.Date,
                    // Manually map the ID to the string for the frontend badge colors
                    subject = r.ReportOptionsId == 1 ? "Pollution" :
                              r.ReportOptionsId == 2 ? "Water Quality" :
                              r.ReportOptionsId == 3 ? "Algae Bloom" : "Other"
                })
                .ToListAsync();

            return Ok(reports);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error fetching reports: " + ex.Message });
        }
    }

    // POST: api/reports
    // This saves the report when a user clicks submit on the form
    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] ForumReport report)
    {
        try
        {
            report.Date = DateTime.UtcNow;
            _context.ForumReports.Add(report);

            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = report.ReportForumId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to save report: " + ex.InnerException?.Message ?? ex.Message });
        }
    }
}