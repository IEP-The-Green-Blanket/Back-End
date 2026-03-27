using Microsoft.AspNetCore.Mvc;
using GB.Infrastructure;
using GB.Domain.Entities; 

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

    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] ForumReport report)
    {
        report.Date = DateTime.UtcNow; // Ensure date is set
        _context.ForumReports.Add(report);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = report.ReportForumId });
    }
}