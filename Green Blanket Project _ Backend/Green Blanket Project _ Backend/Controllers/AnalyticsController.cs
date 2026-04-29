
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GB.Infrastructure;
using GB.Domain.Entities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Green_Blanket_Project___Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly GreenBlanketDbContext _context;

        public AnalyticsController(GreenBlanketDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 0. FOUNDATION & CHATBOT (Unchanged - Fast Lookups)
        // ==========================================
        [HttpGet("test-connection")]
        public async Task<IActionResult> GetRecentReadings()
        {
            try
            {
                var recentData = await _context.WaterReadings
                  .OrderByDescending(w => w.DateTime)
                  .Take(5)
                  .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates })
                  .ToListAsync();

                if (!recentData.Any()) return NotFound("Connected to DB, but table is empty.");
                return Ok(new { status = "Success", message = "Database linked.", data = recentData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database connection failed: {ex.Message}");
            }
        }

        [HttpGet("chatbot-summary")]
        public async Task<IActionResult> GetChatbotSummary()
        {
            var latest = await _context.WaterReadings
              .AsNoTracking()
              .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
              .OrderByDescending(w => w.DateTime)
              .FirstOrDefaultAsync();

            if (latest == null) return NotFound("No recent water data available.");

            float wqi = CalculateWQI(latest);
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double toxicPercentage = 1 / (Math.Pow(10, (9.25 - latest.PhLevel.Value)) + 1);
            double actualToxicNH3 = ammoniaVal * toxicPercentage;
            double healthRiskBase = Math.Clamp((Math.Max(0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates.Value * 120), 0, 100);

            return Ok(new
            {
                lastUpdated = latest.DateTime,
                summary = new
                {
                    waterHealthScore = Math.Round(wqi, 1),
                    healthGrade = MapToTenLevels(wqi, "grade"),
                    swimSafetyStatus = MapToTenLevels(wqi, "swim"),
                    skinIrritationRisk = MapToTenLevels(healthRiskBase, "irritation"),
                    odorProfile = MapToTenLevels(latest.Phosphates.Value * 100, "odor")
                },
                scientificContext = new
                {
                    phValue = latest.PhLevel,
                    nitrateLevel = latest.Nitrates,
                    phosphateLevel = latest.Phosphates,
                    ammoniaToxicityMgL = Math.Round(actualToxicNH3, 5),
                    livestockSafety = actualToxicNH3 < 0.01 ? "Safe for animals" : "Danger: Toxic to livestock"
                },
                aiGuidelines = new
                {
                    canISwim = wqi > 70 ? "Yes, conditions are optimal." : "Proceed with caution or avoid contact.",
                    healthWarning = healthRiskBase > 50 ? "Warning: High risk of skin rashes or ear infections." : "No significant health risks detected.",
                    currentConcern = latest.Phosphates > 0.1 ? "Heavy nutrient loading detected." : "Nutrient levels are normal."
                }
            });
        }

        // ============================================================================
        // 1. OMNI-DASHBOARD (Optimized Graphing for 144/day)
        // ============================================================================
        [HttpGet("omni-dashboard")]
        public async Task<IActionResult> GetOmniDashboard()
        {
            var latest = await _context.WaterReadings
              .AsNoTracking()
              .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null)
              .OrderByDescending(w => w.DateTime)
              .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient data found to build analytics.");

            DateTime latestUtc = DateTime.SpecifyKind(latest.DateTime, DateTimeKind.Utc);

            // Fetch only necessary columns to save RAM, then group by Day in memory
            var rawHistorical = await _context.WaterReadings
        .AsNoTracking()
        .Where(w => w.DateTime >= latestUtc.AddDays(-30)
            && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
        .Select(w => new { w.DateTime, w.Nitrates, w.Phosphates, w.ElectricalConductivity, w.PhLevel })
        .ToListAsync();

            // Squash ~4,320 points into exactly 30 daily averages for the frontend
            var dailyTrends = rawHistorical
        .GroupBy(w => w.DateTime.Date)
        .OrderBy(g => g.Key)
        .Select(g => new {
            Date = g.Key,
            Nitrates = g.Average(x => x.Nitrates.Value),
            Phosphates = g.Average(x => x.Phosphates.Value),
            EC = g.Average(x => x.ElectricalConductivity ?? 0)
        }).ToList();

            // Math calculations
            float wqi = CalculateWQI(latest);
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double toxicPercentage = 1 / (Math.Pow(10, (9.25 - latest.PhLevel.Value)) + 1);
            double actualToxicNH3 = ammoniaVal * toxicPercentage;
            double expansionRate = (latest.Nitrates.Value * 0.5) + (latest.Phosphates.Value * 2.0);
            string sewageSource = (latest.Nitrates / (ammoniaVal > 0 ? ammoniaVal : 0.01)) > 10 ? "Leached Runoff" : "Active Raw Leak";

            // Limit StdDev calculation to the last 7 days only (approx 1,008 rows max)
            var last7DaysPh = rawHistorical.Where(d => d.DateTime >= latestUtc.AddDays(-7)).Select(d => (double)d.PhLevel.Value).ToList();
            double phVolatility = last7DaysPh.Count > 1 ? CalculateStdDev(last7DaysPh) : 0;
            double healthRiskBase = Math.Clamp((Math.Max(0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates.Value * 120), 0, 100);

            return Ok(new
            {
                timestamp = latest.DateTime,
                touristView = new
                {
                    waterHealthScore = wqi,
                    healthGrade = MapToTenLevels(wqi, "grade"),
                    swimSafety = MapToTenLevels(wqi, "swim"),
                    skinIrritationRisk = MapToTenLevels(healthRiskBase, "irritation"),
                    fishKillLikelihood = MapToTenLevels(actualToxicNH3 * 500, "fish"),
                    odorLevel = MapToTenLevels(latest.Phosphates.Value * 100, "odor")
                },
                residentView = new
                {
                    hyacinthGrowthForecast = $"{Math.Round(expansionRate, 1)}% expansion/day",
                    sewageDetection = sewageSource,
                    stabilityStatus = MapToTenLevels(100 - (phVolatility * 50), "stability"),
                    recommendation = wqi < 35 ? "Critical Warning: Avoid shoreline contact." : "Conditions are currently stable."
                },
                scientificIntelligence = new
                {
                    trophicState = latest.Phosphates > 0.1 ? "Hyper-eutrophic" : "Eutrophic",
                    toxicAmmoniaMgL = Math.Round(actualToxicNH3, 4),
                    redfieldRatio = Math.Round(latest.Nitrates.Value / (latest.Phosphates.Value > 0 ? latest.Phosphates.Value : 0.01), 2),
                    livestockDrinkingSafety = actualToxicNH3 < 0.01 ? "Safe" : "Dangerous",
                    soilSalinityRisk = latest.ElectricalConductivity > 75 ? "High (Crop Stress)" : "Low",
                    rawMetrics = new { ph = latest.PhLevel, nitrates = latest.Nitrates, phosphates = latest.Phosphates, ec = latest.ElectricalConductivity, ammoniaTotal = ammoniaVal }
                },
                graphingData = new
                {
                    labels = dailyTrends.Select(d => d.Date.ToString("MMM dd")),
                    nitrateTrend = dailyTrends.Select(d => Math.Round(d.Nitrates, 2)),
                    phosphateTrend = dailyTrends.Select(d => Math.Round(d.Phosphates, 2)),
                    ecTrend = dailyTrends.Select(d => Math.Round(d.EC, 2))
                }
            });
        }

        // ============================================================================
        // 2. FILTERED HISTORICAL DATA (Smart Resolution Engine applied)
        // ============================================================================
        [HttpGet("history/range")]
        public async Task<IActionResult> GetRangeData([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            DateTime startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            DateTime endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc);
            var finalEnd = endUtc.AddHours(23).AddMinutes(59);
            var durationDays = (finalEnd - startUtc).TotalDays;

            var rawData = await _context.WaterReadings
              .AsNoTracking()
              .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd)
              .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates, w.ElectricalConductivity })
              .ToListAsync();

            if (!rawData.Any()) return NotFound("No data found in selection.");

            object groupedData;

            // SMART AGGREGATION: Prevent UI overload
            if (durationDays > 3)
            {
                // Range > 3 days: Group into 1 data point per DAY
                groupedData = rawData
          .GroupBy(w => w.DateTime.Date)
          .Select(g => new {
              x = g.Key.ToString("yyyy-MM-dd"),
              ph = Math.Round(g.Average(w => w.PhLevel ?? 0), 2),
              nitrates = Math.Round(g.Average(w => w.Nitrates ?? 0), 2),
              phosphates = Math.Round(g.Average(w => w.Phosphates ?? 0), 2),
              ec = Math.Round(g.Average(w => w.ElectricalConductivity ?? 0), 2)
          }).OrderBy(g => g.x).ToList();
            }
            else
            {
                // Range <= 3 days: Group into 1 data point per HOUR
                groupedData = rawData
          .GroupBy(w => new DateTime(w.DateTime.Year, w.DateTime.Month, w.DateTime.Day, w.DateTime.Hour, 0, 0))
          .Select(g => new {
              x = g.Key.ToString("yyyy-MM-dd HH:mm"),
              ph = Math.Round(g.Average(w => w.PhLevel ?? 0), 2),
              nitrates = Math.Round(g.Average(w => w.Nitrates ?? 0), 2),
              phosphates = Math.Round(g.Average(w => w.Phosphates ?? 0), 2),
              ec = Math.Round(g.Average(w => w.ElectricalConductivity ?? 0), 2)
          }).OrderBy(g => g.x).ToList();
            }

            return Ok(new { dataPoints = groupedData });
        }

        // ============================================================
        // 3. ECOSYSTEM TRENDS (Pre-projected to save memory)
        // ============================================================
        [HttpGet("graph-data/critical-trends")]
        public async Task<IActionResult> GetCriticalTrends([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            DateTime startDate = start ?? new DateTime(2026, 02, 01);
            DateTime endDate = end ?? new DateTime(2026, 02, 28);
            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var finalEnd = DateTime.SpecifyKind(endDate, DateTimeKind.Utc).AddHours(23).AddMinutes(59);

            // Project only required fields before downloading from database
            var rawData = await _context.WaterReadings
        .AsNoTracking()
        .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd
            && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
        .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates, w.Ammonia })
        .ToListAsync();

            if (!rawData.Any()) return NotFound("No data found.");

            var cleanTrend = rawData
              .GroupBy(d => d.DateTime.Date)
              .OrderBy(g => g.Key)
              .Select(g => new {
                  Date = g.Key,
                  Ph = g.Average(x => x.PhLevel.Value),
                  Nitrates = g.Average(x => x.Nitrates.Value),
                  Phosphates = g.Average(x => x.Phosphates.Value),
                  Ammonia = g.Average(x => x.Ammonia ?? 0.05)
              }).ToList();

            return Ok(new
            {
                count = cleanTrend.Count,
                labels = cleanTrend.Select(d => d.Date.ToString("MMM dd")),
                vitalityTrend = cleanTrend.Select(d => {
                    float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)d.Ph) * 20f), 0, 100);
                    float nitrateScore = Math.Clamp(100f - ((float)d.Nitrates * 10f), 0, 100);
                    float phosphateScore = Math.Clamp(100f - ((float)d.Phosphates * 500f), 0, 100);
                    return Math.Round((phScore * 0.2f) + (nitrateScore * 0.4f) + (phosphateScore * 0.4f), 1);
                }),
                nutrientTrend = cleanTrend.Select(d => new { nitrates = Math.Round(d.Nitrates, 3), phosphates = Math.Round(d.Phosphates, 3) }),
                safetyTrend = cleanTrend.Select(d => {
                    double toxicPercentage = 1 / (Math.Pow(10, (9.25 - d.Ph)) + 1);
                    return Math.Round(d.Ammonia * toxicPercentage, 4);
                })
            });
        }

        // ============================================================
        // 4. FORENSIC ATTRIBUTION (Optimized Aggregation)
        // ============================================================
        [HttpGet("forensic-attribution")]
        public async Task<IActionResult> GetForensicAttribution([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            DateTime startDate = start ?? new DateTime(2026, 02, 01);
            DateTime endDate = end ?? new DateTime(2026, 02, 28);
            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var finalEnd = DateTime.SpecifyKind(endDate, DateTimeKind.Utc).AddHours(23).AddMinutes(59);

            var query = _context.WaterReadings.AsNoTracking()
              .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd
                  && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null);

            var count = await query.CountAsync();
            if (count == 0) return NotFound("No forensic data found for this range.");

            // Calculate averages at the DB level
            double avgNitrates = await query.AverageAsync(d => d.Nitrates.Value);
            double avgPhosphates = await query.AverageAsync(d => d.Phosphates.Value);
            double avgEC = await query.AverageAsync(d => d.ElectricalConductivity.Value);
            int industrialEvents = await query.CountAsync(d => d.ElectricalConductivity > 85);

            // Only pull the 1 column needed for Variance calculation into RAM
            var phLevels = await query.Select(d => (double)d.PhLevel.Value).ToListAsync();
            double pHVariance = CalculateStdDev(phLevels);

            double overallRatio = avgNitrates / (avgPhosphates > 0 ? avgPhosphates : 0.01);

            return Ok(new
            {
                analysisPeriod = $"{startDate:MMM dd} - {endDate:MMM dd}",
                attributionSummary = new
                {
                    dominantPolluter = overallRatio < 16 ? "Municipal (Wastewater)" : "Agricultural (Fertilizer Runoff)",
                    sewageLoadIndex = Math.Round(avgNitrates * 10, 1),
                    fertilizerLoadIndex = Math.Round(avgPhosphates * 100, 1),
                    industrialRiskLevel = industrialEvents > 5 ? "High" : (industrialEvents > 0 ? "Moderate" : "Low")
                },
                forensicMetrics = new
                {
                    avgRedfieldRatio = Math.Round(overallRatio, 2),
                    illegalDischargeEventsDetected = industrialEvents,
                    ecosystemStabilityIndex = Math.Round(100 - (pHVariance * 40), 1),
                    trophicStatus = avgPhosphates > 0.1 ? "Hyper-eutrophic" : "Eutrophic"
                }
            });
        }

        // ============================================================
        // 5. REMEDIATION PROGRESS (Pushed to Database Aggregation)
        // ============================================================
        [HttpGet("remediation-progress")]
        public async Task<IActionResult> GetRemediationProgress([FromQuery] DateTime? end)
        {
            var latestInDb = await _context.WaterReadings.MaxAsync(w => (DateTime?)w.DateTime) ?? DateTime.UtcNow;
            DateTime currentEnd = end ?? latestInDb;
            DateTime currentStart = currentEnd.AddDays(-7);
            DateTime baselineEnd = currentStart.AddSeconds(-1);
            DateTime baselineStart = baselineEnd.AddDays(-7);

            var currentQuery = _context.WaterReadings.AsNoTracking()
              .Where(w => w.DateTime >= currentStart && w.DateTime <= currentEnd && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null);

            var baselineQuery = _context.WaterReadings.AsNoTracking()
              .Where(w => w.DateTime >= baselineStart && w.DateTime <= baselineEnd && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null);

            int currentCount = await currentQuery.CountAsync();
            int baselineCount = await baselineQuery.CountAsync();

            if (currentCount < 3 || baselineCount < 3) return NotFound("Insufficient data to generate a comparative progress report.");

            // ✅ OPTIMIZATION: Calculate Raw Nutrients directly in DB (Extremely Fast)
            double currentNutrients = await currentQuery.AverageAsync(d => d.Nitrates.Value + d.Phosphates.Value);
            double baselineNutrients = await baselineQuery.AverageAsync(d => d.Nitrates.Value + d.Phosphates.Value);

            // ✅ OPTIMIZATION: Approximate WQI directly in PostgreSQL instead of pulling lists to RAM
            // We average the raw components in the DB, then do the math locally on the 3 averages.
            var currentAvgs = await currentQuery.GroupBy(x => 1).Select(g => new {
                Ph = g.Average(x => x.PhLevel.Value),
                Nit = g.Average(x => x.Nitrates.Value),
                Pho = g.Average(x => x.Phosphates.Value)
            }).FirstOrDefaultAsync();

            var baselineAvgs = await baselineQuery.GroupBy(x => 1).Select(g => new {
                Ph = g.Average(x => x.PhLevel.Value),
                Nit = g.Average(x => x.Nitrates.Value),
                Pho = g.Average(x => x.Phosphates.Value)
            }).FirstOrDefaultAsync();

            // Perform the WQI math locally using the DB-calculated averages
            double currentWqi = 0;
            if (currentAvgs != null)
            {
                double phScoreC = Math.Clamp(100 - (Math.Abs(7.5 - currentAvgs.Ph) * 20), 0, 100);
                double nitScoreC = Math.Clamp(100 - (currentAvgs.Nit * 10), 0, 100);
                double phoScoreC = Math.Clamp(100 - (currentAvgs.Pho * 500), 0, 100);
                currentWqi = (phScoreC * 0.2) + (nitScoreC * 0.4) + (phoScoreC * 0.4);
            }

            double baselineWqi = 0;
            if (baselineAvgs != null)
            {
                double phScoreB = Math.Clamp(100 - (Math.Abs(7.5 - baselineAvgs.Ph) * 20), 0, 100);
                double nitScoreB = Math.Clamp(100 - (baselineAvgs.Nit * 10), 0, 100);
                double phoScoreB = Math.Clamp(100 - (baselineAvgs.Pho * 500), 0, 100);
                baselineWqi = (phScoreB * 0.2) + (nitScoreB * 0.4) + (phoScoreB * 0.4);
            }

            double healthDelta = ((currentWqi - baselineWqi) / (baselineWqi > 0 ? baselineWqi : 1)) * 100;
            double pollutionDelta = ((currentNutrients - baselineNutrients) / (baselineNutrients > 0 ? baselineNutrients : 1)) * 100;

            return Ok(new
            {
                comparisonWindow = $"{baselineStart:MMM dd} vs {currentStart:MMM dd}",
                summary = new { ecosystemStatus = healthDelta > 5 ? "Improving" : (healthDelta < -5 ? "Declining" : "Stable"), remediationConfidence = currentCount > 100 ? "High" : "Moderate" },
                vitalityImprovement = new { baselineScore = Math.Round(baselineWqi, 1), currentScore = Math.Round(currentWqi, 1), percentageGain = Math.Round(healthDelta, 1), indicator = healthDelta >= 0 ? "UP" : "DOWN" },
                pollutionReduction = new { baselineLoad = Math.Round(baselineNutrients, 2), currentLoad = Math.Round(currentNutrients, 2), percentageReduced = Math.Round(-pollutionDelta, 1), isTargetMet = currentNutrients < baselineNutrients }
            });
        }

        // ============================================================
        // 6-10. STATIC/LATEST READINGS (No changes needed, fetching single latest item is instant)
        // ============================================================

        [HttpGet("bloom-forecast")]
        public async Task<IActionResult> GetBloomForecast()
        {
            var latestDate = await _context.WaterReadings.MaxAsync(w => (DateTime?)w.DateTime) ?? DateTime.UtcNow;

            // 48 hours * 144 = 288 points. Perfectly safe for memory.
            var recentWindow = await _context.WaterReadings.AsNoTracking()
        .Where(w => w.DateTime >= latestDate.AddDays(-2) && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
        .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates })
        .ToListAsync();

            if (recentWindow.Count < 2) return NotFound("Insufficient recent data points.");

            var sortedWindow = recentWindow.OrderBy(w => w.DateTime).ToList();
            var latestRecord = sortedWindow.Last();

            double nutrientFuel = (latestRecord.Phosphates.Value * 10) + (latestRecord.Nitrates.Value / 2);
            double phMax = recentWindow.Max(w => w.PhLevel.Value);
            double phMin = recentWindow.Min(w => w.PhLevel.Value);
            double phShift = phMax - phMin;
            double probability = Math.Clamp((nutrientFuel * 5) + (phShift * 60), 0, 100);

            return Ok(new { riskMetrics = new { bloomProbability = Math.Round(probability, 1) + "%", currentRiskLevel = MapToTenLevels(probability, "risk") } });
        }

        [HttpGet("irrigation-safety")]
        public async Task<IActionResult> GetIrrigationSafety()
        {
            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.Calcium != null && w.Magnesium != null && w.Sodium != null && w.ElectricalConductivity != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient mineral data.");

            double sar = (latest.Sodium.Value / 22.99) / Math.Sqrt(((latest.Calcium.Value / 20.04) + (latest.Magnesium.Value / 12.15)) / 2);
            return Ok(new { soilHealthMetrics = new { sodiumAdsorptionRatio = Math.Round(sar, 2) } });
        }

        [HttpGet("infrastructure-risk")]
        public async Task<IActionResult> GetInfrastructureRisk()
        {
            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.Chloride != null && w.Sulfate != null && w.TotalAlkalinity != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient mineral data.");

            double alk_meq = latest.TotalAlkalinity.Value / 50.04;
            double larsonIndex = ((latest.Chloride.Value / 35.45) + ((latest.Sulfate.Value / 96.06) * 2)) / (alk_meq > 0 ? alk_meq : 0.01);
            return Ok(new { corrosivityAnalysis = new { larsonSkoldIndex = Math.Round(larsonIndex, 2) } });
        }

        [HttpGet("nutrient-harvest-value")]
        public async Task<IActionResult> GetHarvestValue()
        {
            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.Nitrates != null && w.Phosphates != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound("Data unavailable.");

            double financialValue = (((latest.Nitrates.Value * 195000000) / 1000000) * 18500.00 + ((latest.Phosphates.Value * 195000000) / 1000000) * 24000.00) * 0.15;
            return Ok(new { marketValue = new { estimatedHarvestValue = Math.Round(financialValue, 2) } });
        }

        [HttpGet("regulatory-compliance")]
        public async Task<IActionResult> GetComplianceStatus()
        {
            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.Ammonia != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient data.");

            double score = 100 - (latest.PhLevel.Value < 6.5 || latest.PhLevel.Value > 9.0 ? 15 : 0) - (latest.Phosphates.Value > 0.05 ? 35 : 0) - (latest.Nitrates.Value > 6.0 ? 25 : 0) - (latest.Ammonia.Value > 0.02 ? 25 : 0);
            return Ok(new { complianceOverview = new { overallComplianceScore = Math.Max(0, score) } });
        }

        // ============================================================
        // 11. MASTER STATISTICAL AUDIT (The Critical RAM Fix)
        // ============================================================
        [HttpGet("master-audit")]
        public async Task<IActionResult> GetMasterAudit([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            var query = _context.WaterReadings.AsNoTracking();

            if (start.HasValue) query = query.Where(w => w.DateTime >= DateTime.SpecifyKind(start.Value, DateTimeKind.Utc));
            if (end.HasValue) query = query.Where(w => w.DateTime <= DateTime.SpecifyKind(end.Value, DateTimeKind.Utc));

            // CRITICAL FIX: Do NOT pull 150k+ rows into memory. Use Database-side math.
            int totalRecords = await query.CountAsync();
            if (totalRecords == 0) return NotFound("No historical records found.");

            var minDate = await query.MinAsync(w => w.DateTime);
            var maxDate = await query.MaxAsync(w => w.DateTime);

            var maxPh = await query.MaxAsync(w => w.PhLevel) ?? 0;
            var maxNitrate = await query.MaxAsync(w => w.Nitrates) ?? 0;
            var maxPhosphate = await query.MaxAsync(w => w.Phosphates) ?? 0;
            var maxAmmonia = await query.MaxAsync(w => w.Ammonia) ?? 0;

            // Integrity Checks
            int phCount = await query.CountAsync(d => d.PhLevel != null);
            int nCount = await query.CountAsync(d => d.Nitrates != null);
            int pCount = await query.CountAsync(d => d.Phosphates != null);
            int aCount = await query.CountAsync(d => d.Ammonia != null);

            double completeness = ((double)(phCount + nCount + pCount + aCount) / (totalRecords * 4)) * 100;

            // Variance logic (Pulling just ONE small column to save RAM)
            var phList = await query.Where(d => d.PhLevel != null).Select(d => (double)d.PhLevel.Value).ToListAsync();
            var nList = await query.Where(d => d.Nitrates != null).Select(d => (double)d.Nitrates.Value).ToListAsync();

            double phStability = CalculateStdDev(phList);
            double nutrientVolatility = CalculateStdDev(nList);

            return Ok(new
            {
                auditMetadata = new { totalRecordsAnalyzed = totalRecords, earliestEntry = minDate, latestEntry = maxDate, reportGeneratedAt = DateTime.Now },
                historicalExtremes = new { highestAlkalinityRecorded = maxPh, peakSewageInflowMgL = maxNitrate, peakFertilizerInflowMgL = maxPhosphate, lethalAmmoniaSpikeMgL = maxAmmonia },
                ecosystemVariance = new { phVarianceIndex = Math.Round(phStability, 3), nutrientVolatilityScore = Math.Round(nutrientVolatility, 3), description = "High variance indicates active distress." },
                databaseHealth = new { completenessPercentage = Math.Round(completeness, 1) + "%", sensorHealthStatus = totalRecords > 50 ? "High Fidelity" : "Low Resolution", isUsableForModeling = completeness > 85 },
                scientificConclusion = $"Analysis of {totalRecords} points indicates the ecosystem is {(phStability > 0.6 ? "undergoing rapid shifts" : "stable")}."
            });
        }

        // ==========================================
        // HELPERS (Unchanged)
        // ==========================================
        private string MapToTenLevels(double score, string type)
        {
            int index = (int)Math.Clamp(Math.Floor(score / 10), 0, 9);
            return type.ToLower() switch
            {
                "grade" => new[] { "1: Hazardous", "2: Critical", "3: Very Poor", "4: Poor", "5: Fair", "6: Average", "7: Good", "8: Very Good", "9: Excellent", "10: Pristine" }[index],
                "swim" => new[] { "Prohibited", "Highly Dangerous", "Danger", "Unsafe", "Not Recommended", "Caution", "Marginal", "Fair", "Safe", "Ideal" }[index],
                "irritation" => new[] { "None", "Negligible", "Low", "Mild", "Moderate", "Noticeable", "High", "Very High", "Extreme", "Severe" }[index],
                "fish" => new[] { "None", "Very Low", "Unlikely", "Possible", "Probable", "High", "Very High", "Critical", "Massive", "Total Collapse" }[index],
                "odor" => new[] { "Fresh", "Neutral", "Earthy", "Musty", "Noticeable", "Strong", "Unpleasant", "Pungent", "Severe", "Overwhelming" }[index],
                "stability" => new[] { "Chaotic", "Highly Volatile", "Unstable", "Shifting", "Fluctuating", "Improving", "Steadying", "Stable", "Highly Stable", "Rock Solid" }[index],
                "risk" => new[] { "Inert", "Negligible", "Very Low", "Low", "Moderate", "Elevated", "High", "Very High", "Extreme", "Outbreak Imminent" }[index],
                _ => "Information Unavailable"
            };
        }

        private float CalculateWQI(WaterReading data)
        {
            if (data.PhLevel == null || data.Nitrates == null || data.Phosphates == null) return 0;
            float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)data.PhLevel.Value) * 20f), 0, 100);
            float nitrateScore = Math.Clamp(100f - ((float)data.Nitrates.Value * 10f), 0, 100);
            float phosphateScore = Math.Clamp(100f - ((float)data.Phosphates.Value * 500f), 0, 100);
            return (float)Math.Round((phScore * 0.2f) + (nitrateScore * 0.4f) + (phosphateScore * 0.4f), 1);
        }

        private double CalculateStdDev(List<double> values)
        {
            if (values == null || values.Count < 2) return 0;
            double avg = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }
    }
}