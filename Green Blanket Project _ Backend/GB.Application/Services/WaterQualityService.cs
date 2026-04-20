using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GB.Application.DTOs;
using GB.Infrastructure; // Matches your provided DbContext namespace
using GB.Domain.Entities;   // Matches your WaterReading entity namespace

namespace GB.Application.Services
{
    public class WaterQualityService
    {
        private readonly GreenBlanketDbContext _context; // Fixed: Using your specific class name

        public WaterQualityService(GreenBlanketDbContext context)
        {
            _context = context;
        }

        // --- INTELLIGENCE ENGINE (The "Fixer" Logic) ---

        public async Task<WaterReading?> GetLatestReadingAsync()
        {
            // Explicitly referencing WaterReadings table fixes 'TEntity' errors
            return await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();
        }

        public async Task<object?> GetAiStatusPacketAsync()
        {
            var latest = await GetLatestReadingAsync();
            if (latest == null) return null;

            float wqi = CalculateWQI(latest);
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double toxicPercentage = 1 / (Math.Pow(10, (9.25 - latest.PhLevel.Value)) + 1);
            double actualToxicNH3 = ammoniaVal * toxicPercentage;

            // Health risk based on pH and high Phosphates
            double healthRiskBase = Math.Clamp((Math.Max(0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates.Value * 120), 0, 100);

            return new
            {
                timestamp = latest.DateTime,
                healthScore = Math.Round(wqi, 1),
                healthGrade = MapToTenLevels(wqi, "grade"),
                swimSafety = MapToTenLevels(wqi, "swim"),
                skinIrritationRisk = MapToTenLevels(healthRiskBase, "irritation"),
                livestockSafety = actualToxicNH3 < 0.01 ? "Safe" : "Dangerous",
                hyacinthForecast = $"{Math.Round((latest.Nitrates.Value * 0.5) + (latest.Phosphates.Value * 2.0), 1)}% expansion/day"
            };
        }

        public float CalculateWQI(WaterReading reading)
        {
            // Calculation logic using the properties from your WaterReading class
            double phScore = 100 - (Math.Abs(7.5 - (double)reading.PhLevel!) * 20);
            double nitScore = 100 - ((double)reading.Nitrates! * 10);
            double phoScore = 100 - ((double)reading.Phosphates! * 500);

            double wqi = (phScore * 0.2) + (nitScore * 0.4) + (phoScore * 0.4);
            return (float)Math.Clamp(wqi, 0, 100);
        }

        public string MapToTenLevels(double score, string category)
        {
            int level = (int)Math.Clamp(Math.Floor(score / 10), 0, 9);
            var grades = new[] { "Crisis", "Severe", "Poor", "Marginal", "Fair", "Average", "Good", "Great", "Pristine", "Optimal" };
            var swim = new[] { "Lethal", "Dangerous", "Unsafe", "Poor", "Marginal", "Acceptable", "Good", "Great", "Safe", "Perfect" };
            var irritation = new[] { "None", "None", "Low", "Mild", "Mild", "Moderate", "High", "High", "Critical", "Hazardous" };

            return category switch { "grade" => grades[level], "swim" => swim[level], "irritation" => irritation[level], _ => "Unknown" };
        }

        // --- LEGACY COMPATIBILITY METHODS (Fixes compilation errors in old controllers) ---

        public string GetRandomStatus()
        {
            return "Live Data System Active";
        }

        public WaterChemicalsDto GetRandomChemicals()
        {
            return new WaterChemicalsDto { Nitrate = 0, Phosphate = 0, Oxygen = 0 };
        }
    }
}