using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GB.Application.DTOs;
using GB.Infrastructure;
using GB.Domain.Entities;

namespace GB.Application.Services
{
    public class WaterQualityService
    {
        private readonly GreenBlanketDbContext _context;

        public WaterQualityService(GreenBlanketDbContext context)
        {
            _context = context;
        }

        public async Task<WaterReading?> GetLatestReadingAsync()
        {
            return await _context.WaterReadings
                .AsNoTracking()
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();
        }

        public async Task<object?> GetAiStatusPacketAsync()
{
    var latest = await GetLatestReadingAsync();
    if (latest == null) return null;

    // 1. CALCULATE TOXICITY (Ammonia vs pH relationship)
    double ammoniaVal = latest.Ammonia ?? 0.05;
    double phVal = latest.PhLevel ?? 7.0;
    double toxicPercentage = 1 / (Math.Pow(10, (9.25 - phVal)) + 1);
    double actualToxicNH3 = ammoniaVal * toxicPercentage;

    // 2. CALCULATE LARSON-SKOLD INDEX (Boating/Motor Corrosion)
    // Updated to match your exact entity names: Chloride, Sulfate, TotalAlkalinity
    double chlorides = latest.Chloride ?? 0; 
    double sulfates = latest.Sulfate ?? 0;
    double alkalinity = latest.TotalAlkalinity ?? 100; 
    double larsonIndex = (chlorides + sulfates) / alkalinity;

    // 3. CALCULATE SAR (Sodium Adsorption Ratio for Farming)
    // Matches your entity names: Sodium, Calcium, Magnesium
    double sodium = latest.Sodium ?? 0;
    double calcium = latest.Calcium ?? 0;
    double magnesium = latest.Magnesium ?? 0;
    double sarValue = (calcium + magnesium) > 0 
        ? sodium / Math.Sqrt((calcium + magnesium) / 2) 
        : 0;

    // 4. CALCULATE RECREATIONAL RISKS
    float wqi = CalculateWQI(latest);
    double phosphates = latest.Phosphates ?? 0;
    double healthRiskBase = Math.Clamp((Math.Max(0, phVal - 7.0) * 15) + (phosphates * 120), 0, 100);

    // 5. CONSTRUCT THE INTELLIGENCE PACKET
    return new
    {
        timestamp = latest.DateTime,
        waterHealthScore = Math.Round(wqi, 1),
        healthGrade = MapToTenLevels(wqi, "grade"),
        swimSafety = MapToTenLevels(wqi, "swim"),
        skinIrritationRisk = MapToTenLevels(healthRiskBase, "irritation"),
        fishKillLikelihood = actualToxicNH3 > 0.02 || phVal > 9.2 ? "High" : "Low",
        larsonSkoldIndex = Math.Round(larsonIndex, 2),
        motorCorrosionRisk = larsonIndex > 1.2 ? "High" : "Low",
        hyacinthGrowthForecast = $"{Math.Round(((latest.Nitrates ?? 0) * 0.5) + (phosphates * 2.0), 1)}% expansion/day",
        sodiumAdsorptionRatio = Math.Round(sarValue, 2),
        soilSalinityRisk = (latest.ElectricalConductivity ?? 0) > 150 ? "High" : "Low",
        irrigationSafety = sarValue < 3.0 ? "Optimal" : "Caution",
        toxicAmmoniaMgL = Math.Round(actualToxicNH3, 4),
        livestockSafety = actualToxicNH3 < 0.01 ? "Safe" : "Dangerous"
    };
}

        public float CalculateWQI(WaterReading reading)
        {
            double phScore = 100 - (Math.Abs(7.5 - (reading.PhLevel ?? 7.5)) * 20);
            double nitScore = 100 - ((reading.Nitrates ?? 0) * 10);
            double phoScore = 100 - ((reading.Phosphates ?? 0) * 500);

            double wqi = (phScore * 0.2) + (nitScore * 0.4) + (phoScore * 0.4);
            return (float)Math.Clamp(wqi, 0, 100);
        }

        public string MapToTenLevels(double score, string category)
        {
            int level = (int)Math.Clamp(Math.Floor(score / 10), 0, 9);
            var grades = new[] { "Crisis", "Severe", "Poor", "Marginal", "Fair", "Average", "Good", "Great", "Pristine", "Optimal" };
            var swim = new[] { "Lethal", "Dangerous", "Unsafe", "Poor", "Marginal", "Acceptable", "Good", "Great", "Safe", "Perfect" };
            var irritation = new[] { "None", "None", "Low", "Mild", "Mild", "Moderate", "High", "High", "Critical", "Hazardous" };

            return category switch
            {
                "grade" => grades[level],
                "swim" => swim[level],
                "irritation" => irritation[level],
                _ => "Unknown"
            };
        }

        // --- LEGACY COMPATIBILITY ---
        public string GetRandomStatus() => "Intelligence Engine Online";
        public WaterChemicalsDto GetRandomChemicals() => new WaterChemicalsDto { Nitrate = 0, Phosphate = 0, Oxygen = 0 };
    }
}