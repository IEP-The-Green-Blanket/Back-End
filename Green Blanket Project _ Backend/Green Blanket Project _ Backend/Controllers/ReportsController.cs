using GB.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using GB.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Linq; // Added for .ToList()

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
        // ==========================================
        // FIXER DIAGNOSTIC: THE TABLE HUNT
        // ==========================================
        try
        {
            var tables = _context.Database.SqlQueryRaw<string>(
                @"SELECT table_schema || '.' || table_name 
                  FROM information_schema.tables 
                  WHERE table_type = 'BASE TABLE' 
                  AND table_schema NOT IN ('information_schema', 'pg_catalog')"
            ).ToList();

            Console.WriteLine("\n==========================================");
            Console.WriteLine($">>> DATABASE: {_context.Database.GetDbConnection().Database}");
            Console.WriteLine(">>> I CAN SEE THESE TABLES: " + (tables.Any() ? string.Join(", ", tables) : "NONE FOUND"));
            Console.WriteLine("==========================================\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> DIAGNOSTIC FAILED: {ex.Message}");
        }

        // ==========================================
        // EXISTING LOGIC
        // ==========================================
        report.Date = DateTime.UtcNow;
        _context.ForumReports.Add(report);

        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = report.ReportForumId });
    }
}