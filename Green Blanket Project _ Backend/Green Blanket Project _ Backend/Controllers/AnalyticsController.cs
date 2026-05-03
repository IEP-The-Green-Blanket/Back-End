using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;

        public AnalyticsController(GreenBlanketDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> GetRecentReadings()
        {
            const string cacheKey = "RecentReadings_Test";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            try
            {
                var recentData = await _context.WaterReadings.AsNoTracking()
                    .OrderByDescending(w => w.DateTime).Take(5)
                    .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates })
                    .ToListAsync();

                var response = new { status = "Success", data = recentData };
                _cache.Set(cacheKey, response, TimeSpan.FromSeconds(30));
                return Ok(response);
            }
            catch (Exception ex) { return StatusCode(500, $"DB Error: {ex.Message}"); }
        }

        [HttpGet("chatbot-summary")]
        public async Task<IActionResult> GetChatbotSummary()
        {
            const string cacheKey = "ChatbotSummary";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound();

            float wqi = CalculateWQI(latest);
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double actualToxicNH3 = ammoniaVal * (1 / (Math.Pow(10, (9.25 - latest.PhLevel!.Value)) + 1));
            double healthRiskBase = Math.Clamp((Math.Max(0.0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates!.Value * 120), 0.0, 100.0);

            var result = new
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
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Ok(result);
        }

        [HttpGet("omni-dashboard")]
        public async Task<IActionResult> GetOmniDashboard()
        {
            const string cacheKey = "OmniDashboardData";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var latest = await _context.WaterReadings.AsNoTracking()
              .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null)
              .OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient data.");

            DateTime latestUtc = DateTime.SpecifyKind(latest.DateTime, DateTimeKind.Utc);

            // FIX 1: Let Database group the heavy 30-day graphs
            var dailyTrends = await _context.WaterReadings.AsNoTracking()
                .Where(w => w.DateTime >= latestUtc.AddDays(-30) && w.PhLevel != null)
                .GroupBy(w => w.DateTime.Date)
                .Select(g => new {
                    Date = g.Key,
                    Nitrates = g.Average(x => x.Nitrates ?? 0),
                    Phosphates = g.Average(x => x.Phosphates ?? 0),
                    EC = g.Average(x => x.ElectricalConductivity ?? 0)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            float wqi = CalculateWQI(latest);
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double actualToxicNH3 = ammoniaVal * (1 / (Math.Pow(10, (9.25 - latest.PhLevel!.Value)) + 1));
            double expRate = (latest.Nitrates!.Value * 0.5) + (latest.Phosphates!.Value * 2.0);
            string sewageSrc = (latest.Nitrates.Value / (ammoniaVal > 0 ? ammoniaVal : 0.01)) > 10 ? "Leached Runoff" : "Active Raw Leak";

            // FIX 1b: Pull only the raw pH floats needed for volatility math (Uses ~40kb RAM instead of massive objects)
            var last7DaysPh = await _context.WaterReadings.AsNoTracking()
                .Where(d => d.DateTime >= latestUtc.AddDays(-7) && d.PhLevel != null)
                .Select(d => (double)d.PhLevel!.Value)
                .ToListAsync();

            double volatility = last7DaysPh.Count > 1 ? CalculateStdDev(last7DaysPh) : 0;
            double healthRisk = Math.Clamp((Math.Max(0.0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates.Value * 120), 0.0, 100.0);

            var finalPayload = new
            {
                timestamp = latest.DateTime,
                touristView = new
                {
                    waterHealthScore = wqi,
                    healthGrade = MapToTenLevels(wqi, "grade"),
                    swimSafety = MapToTenLevels(wqi, "swim"),
                    skinIrritationRisk = MapToTenLevels(healthRisk, "irritation"),
                    fishKillLikelihood = MapToTenLevels(actualToxicNH3 * 500, "fish"),
                    odorLevel = MapToTenLevels(latest.Phosphates.Value * 100, "odor")
                },
                residentView = new
                {
                    hyacinthGrowthForecast = $"{Math.Round(expRate, 1)}% expansion/day",
                    sewageDetection = sewageSrc,
                    stabilityStatus = MapToTenLevels(100 - (volatility * 50), "stability"),
                    recommendation = wqi < 35 ? "Critical Warning: Avoid shoreline contact." : "Conditions stable"
                },
                scientificIntelligence = new
                {
                    trophicState = latest.Phosphates.Value > 0.1 ? "Hyper-eutrophic" : "Eutrophic",
                    toxicAmmoniaMgL = Math.Round(actualToxicNH3, 4),
                    redfieldRatio = Math.Round(latest.Nitrates.Value / (latest.Phosphates.Value > 0 ? latest.Phosphates.Value : 0.01), 2),
                    livestockDrinkingSafety = actualToxicNH3 < 0.01 ? "Safe" : "Dangerous",
                    soilSalinityRisk = latest.ElectricalConductivity!.Value > 75 ? "High" : "Low",
                    rawMetrics = new { ph = latest.PhLevel, nitrates = latest.Nitrates, phosphates = latest.Phosphates, ec = latest.ElectricalConductivity, ammoniaTotal = ammoniaVal }
                },
                graphingData = new
                {
                    labels = dailyTrends.Select(d => d.Date.ToString("MMM dd")),
                    nitrateTrend = dailyTrends.Select(d => Math.Round(d.Nitrates, 2)),
                    phosphateTrend = dailyTrends.Select(d => Math.Round(d.Phosphates, 2)),
                    ecTrend = dailyTrends.Select(d => Math.Round(d.EC, 2))
                }
            };

            _cache.Set(cacheKey, finalPayload, TimeSpan.FromMinutes(5));
            return Ok(finalPayload);
        }

        [HttpGet("history/range")]
        public async Task<IActionResult> GetRangeData([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            string cacheKey = $"HistoryRange_{start}_{end}";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            DateTime endDate = end ?? DateTime.UtcNow;
            DateTime startDate = start ?? endDate.AddDays(-7);

            // Hard Cap: Prevent users from requesting massive ranges and crashing the DB
            if ((endDate - startDate).TotalDays > 180) return BadRequest("Date range cannot exceed 6 months.");

            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var finalEnd = DateTime.SpecifyKind(endDate, DateTimeKind.Utc).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            double durationDays = (finalEnd - startUtc).TotalDays;

            object groupedData;

            // FIX 2: Grouping in DB layer before ToListAsync
            if (durationDays > 3)
            {
                var dbAggregated = await _context.WaterReadings.AsNoTracking()
                    .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd)
                    .GroupBy(w => w.DateTime.Date)
                    .Select(g => new {
                        Date = g.Key,
                        ph = g.Average(w => w.PhLevel ?? 0),
                        nitrates = g.Average(w => w.Nitrates ?? 0),
                        phosphates = g.Average(w => w.Phosphates ?? 0),
                        ec = g.Average(w => w.ElectricalConductivity ?? 0)
                    })
                    .OrderBy(g => g.Date)
                    .ToListAsync();

                groupedData = dbAggregated.Select(g => new {
                    x = g.Date.ToString("yyyy-MM-dd"),
                    ph = Math.Round(g.ph, 2),
                    nitrates = Math.Round(g.nitrates, 2),
                    phosphates = Math.Round(g.phosphates, 2),
                    ec = Math.Round(g.ec, 2)
                }).ToList();
            }
            else
            {
                var rawData = await _context.WaterReadings.AsNoTracking()
                    .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd)
                    .Select(w => new { w.DateTime, w.PhLevel, w.Nitrates, w.Phosphates, w.ElectricalConductivity })
                    .ToListAsync();

                groupedData = rawData.GroupBy(w => new DateTime(w.DateTime.Year, w.DateTime.Month, w.DateTime.Day, w.DateTime.Hour, 0, 0))
                    .Select(g => new {
                        x = g.Key.ToString("yyyy-MM-dd HH:mm"),
                        ph = Math.Round(g.Average(w => w.PhLevel ?? 0), 2),
                        nitrates = Math.Round(g.Average(w => w.Nitrates ?? 0), 2),
                        phosphates = Math.Round(g.Average(w => w.Phosphates ?? 0), 2),
                        ec = Math.Round(g.Average(w => w.ElectricalConductivity ?? 0), 2)
                    }).OrderBy(g => g.x).ToList();
            }

            var response = new { dataPoints = groupedData };
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(10));
            return Ok(response);
        }

        [HttpGet("graph-data/critical-trends")]
        public async Task<IActionResult> GetCriticalTrends([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            const string cacheKey = "CriticalTrends";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            DateTime endDate = end ?? DateTime.UtcNow;
            DateTime startDate = start ?? endDate.AddDays(-30);
            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var finalEnd = DateTime.SpecifyKind(endDate, DateTimeKind.Utc).Date.AddHours(23).AddMinutes(59);

            // FIX 3: Push daily grouping straight to the Postgres database
            var dbAggregated = await _context.WaterReadings.AsNoTracking()
                .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .GroupBy(d => d.DateTime.Date)
                .Select(g => new {
                    Date = g.Key,
                    Ph = g.Average(x => x.PhLevel!.Value),
                    Nit = g.Average(x => x.Nitrates!.Value),
                    Pho = g.Average(x => x.Phosphates!.Value),
                    Amm = g.Average(x => x.Ammonia ?? 0.05)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            if (!dbAggregated.Any()) return NotFound();

            var payload = new
            {
                labels = dbAggregated.Select(d => d.Date.ToString("MMM dd")),
                vitalityTrend = dbAggregated.Select(d => {
                    float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)d.Ph) * 20f), 0f, 100f);
                    float nitrateScore = Math.Clamp(100f - ((float)d.Nit * 10f), 0f, 100f);
                    float phosphateScore = Math.Clamp(100f - ((float)d.Pho * 500f), 0f, 100f);
                    return Math.Round((phScore * 0.2f) + (nitrateScore * 0.4f) + (phosphateScore * 0.4f), 1);
                }),
                nutrientTrend = dbAggregated.Select(d => new { nitrates = Math.Round(d.Nit, 3), phosphates = Math.Round(d.Pho, 3) }),
                safetyTrend = dbAggregated.Select(d => Math.Round(d.Amm * (1 / (Math.Pow(10, (9.25 - d.Ph)) + 1)), 4))
            };

            _cache.Set(cacheKey, payload, TimeSpan.FromMinutes(15));
            return Ok(payload);
        }

        [HttpGet("forensic-attribution")]
        public async Task<IActionResult> GetForensicAttribution([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            string cacheKey = $"Forensic_{start}_{end}";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            DateTime endDate = end ?? DateTime.UtcNow;
            DateTime startDate = start ?? endDate.AddDays(-30);
            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var finalEnd = DateTime.SpecifyKind(endDate, DateTimeKind.Utc).Date.AddHours(23).AddMinutes(59);

            var query = _context.WaterReadings.AsNoTracking()
              .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null);

            if (await query.CountAsync() == 0) return NotFound();

            double avgNit = await query.AverageAsync(d => d.Nitrates!.Value);
            double avgPho = await query.AverageAsync(d => d.Phosphates!.Value);
            int industrial = await query.CountAsync(d => d.ElectricalConductivity!.Value > 85);
            var phList = await query.OrderByDescending(d => d.DateTime).Take(5000).Select(d => (double)d.PhLevel!.Value).ToListAsync();
            double phVar = CalculateStdDev(phList);
            double ratio = avgNit / (avgPho > 0 ? avgPho : 0.01);

            var result = new
            {
                analysisPeriod = $"{startDate:MMM dd} - {endDate:MMM dd}",
                attributionSummary = new { dominantPolluter = ratio < 16 ? "Municipal (Wastewater)" : "Agricultural (Fertilizer Runoff)", industrialRiskLevel = industrial > 5 ? "High" : "Low" },
                forensicMetrics = new { avgRedfieldRatio = Math.Round(ratio, 2), EcosystemStabilityIndex = Math.Round(100 - (phVar * 40), 1), illegalDischargeEventsDetected = industrial }
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(15));
            return Ok(result);
        }

        [HttpGet("remediation-progress")]
        public async Task<IActionResult> GetRemediationProgress([FromQuery] DateTime? end)
        {
            string cacheKey = $"Remediation_{end}";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var latestInDb = await _context.WaterReadings.MaxAsync(w => (DateTime?)w.DateTime) ?? DateTime.UtcNow;
            DateTime curEnd = end ?? latestInDb;
            DateTime curStart = curEnd.AddDays(-7);
            DateTime basStart = curStart.AddDays(-7);

            var cQ = _context.WaterReadings.AsNoTracking().Where(w => w.DateTime >= curStart && w.DateTime <= curEnd && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null);
            var bQ = _context.WaterReadings.AsNoTracking().Where(w => w.DateTime >= basStart && w.DateTime < curStart && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null);

            if (await cQ.CountAsync() < 3 || await bQ.CountAsync() < 3) return NotFound("Insufficient data.");

            var cA = await cQ.GroupBy(x => 1).Select(g => new { Ph = g.Average(x => x.PhLevel!.Value), Nit = g.Average(x => x.Nitrates!.Value), Pho = g.Average(x => x.Phosphates!.Value) }).FirstOrDefaultAsync();
            var bA = await bQ.GroupBy(x => 1).Select(g => new { Ph = g.Average(x => x.PhLevel!.Value), Nit = g.Average(x => x.Nitrates!.Value), Pho = g.Average(x => x.Phosphates!.Value) }).FirstOrDefaultAsync();

            if (cA == null || bA == null) return NotFound();

            double cWqi = (Math.Clamp(100.0 - (Math.Abs(7.5 - cA.Ph) * 20.0), 0.0, 100.0) * 0.2) + (Math.Clamp(100.0 - (cA.Nit * 10.0), 0.0, 100.0) * 0.4) + (Math.Clamp(100.0 - (cA.Pho * 500.0), 0.0, 100.0) * 0.4);
            double bWqi = (Math.Clamp(100.0 - (Math.Abs(7.5 - bA.Ph) * 20.0), 0.0, 100.0) * 0.2) + (Math.Clamp(100.0 - (bA.Nit * 10.0), 0.0, 100.0) * 0.4) + (Math.Clamp(100.0 - (bA.Pho * 500.0), 0.0, 100.0) * 0.4);

            var result = new
            {
                summary = new { ecosystemStatus = cWqi > bWqi ? "Improving" : "Declining" },
                vitalityImprovement = new { baselineScore = Math.Round(bWqi, 1), currentScore = Math.Round(cWqi, 1), percentageGain = Math.Round(((cWqi - bWqi) / (bWqi > 0 ? bWqi : 1)) * 100, 1) },
                pollutionReduction = new { baselineLoad = Math.Round(bA.Nit + bA.Pho, 2), currentLoad = Math.Round(cA.Nit + cA.Pho, 2), isTargetMet = (cA.Nit + cA.Pho) < (bA.Nit + bA.Pho), percentageReduced = Math.Round(((bA.Nit + bA.Pho) - (cA.Nit + cA.Pho)) / (bA.Nit + bA.Pho) * 100, 1) }
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        [HttpGet("bloom-forecast")]
        public async Task<IActionResult> GetBloomForecast()
        {
            const string cacheKey = "BloomForecast";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var win = await _context.WaterReadings.AsNoTracking().OrderByDescending(w => w.DateTime).Take(288).ToListAsync();
            if (win.Count < 2) return NotFound();
            var latest = win.First();
            double nutrientFuel = (latest.Phosphates!.Value * 10) + (latest.Nitrates!.Value / 2);
            double prob = Math.Clamp((nutrientFuel * 5) + ((win.Max(w => w.PhLevel!.Value) - win.Min(w => w.PhLevel!.Value)) * 60), 0.0, 100.0);

            var result = new { riskMetrics = new { bloomProbability = Math.Round(prob, 1) + "%", currentRiskLevel = MapToTenLevels(prob, "risk") } };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        [HttpGet("irrigation-safety")]
        public async Task<IActionResult> GetIrrigationSafety()
        {
            const string cacheKey = "IrrigationSafety";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var l = await _context.WaterReadings.AsNoTracking().Where(w => w.Calcium != null && w.Sodium != null && w.Magnesium != null).OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();
            if (l == null) return NotFound();
            double sar = (l.Sodium!.Value / 22.99) / Math.Sqrt(((l.Calcium!.Value / 20.04) + (l.Magnesium!.Value / 12.15)) / 2);

            var result = new { soilHealthMetrics = new { sodiumAdsorptionRatio = Math.Round(sar, 2) } };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        [HttpGet("infrastructure-risk")]
        public async Task<IActionResult> GetInfrastructureRisk()
        {
            const string cacheKey = "InfraRisk";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var l = await _context.WaterReadings.AsNoTracking().Where(w => w.Chloride != null && w.Sulfate != null && w.TotalAlkalinity != null).OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();
            if (l == null) return NotFound();
            double alk_meq = l.TotalAlkalinity!.Value / 50.04;
            double larson = ((l.Chloride!.Value / 35.45) + ((l.Sulfate!.Value / 96.06) * 2)) / (alk_meq > 0 ? alk_meq : 0.01);

            var result = new { corrosivityAnalysis = new { larsonSkoldIndex = Math.Round(larson, 2) } };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        [HttpGet("nutrient-harvest-value")]
        public async Task<IActionResult> GetHarvestValue()
        {
            const string cacheKey = "HarvestValue";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var l = await _context.WaterReadings.AsNoTracking().Where(w => w.Nitrates != null && w.Phosphates != null).OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();
            if (l == null) return NotFound();
            double val = (((l.Nitrates!.Value * 195000000) / 1000000) * 18500.00 + ((l.Phosphates!.Value * 195000000) / 1000000) * 24000.00) * 0.15;

            var result = new { marketValue = new { estimatedHarvestValue = Math.Round(val, 2) } };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
            return Ok(result);
        }

        [HttpGet("regulatory-compliance")]
        public async Task<IActionResult> GetComplianceStatus()
        {
            const string cacheKey = "ComplianceStatus";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var l = await _context.WaterReadings.AsNoTracking().Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.Ammonia != null).OrderByDescending(w => w.DateTime).FirstOrDefaultAsync();
            if (l == null) return NotFound();
            double s = 100.0 - (l.PhLevel!.Value < 6.5 || l.PhLevel.Value > 9.0 ? 15 : 0) - (l.Phosphates!.Value > 0.05 ? 35 : 0) - (l.Nitrates!.Value > 6.0 ? 25 : 0) - (l.Ammonia!.Value > 0.02 ? 25 : 0);

            var result = new { complianceOverview = new { overallComplianceScore = Math.Max(0.0, s) } };
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        [HttpGet("master-audit")]
        public async Task<IActionResult> GetMasterAudit([FromQuery] DateTime? start, [FromQuery] DateTime? end, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            pageNumber = Math.Max(1, pageNumber);

            string cacheKey = $"MasterAuditData_Daily_{start}_{end}_{pageNumber}_{pageSize}";
            if (_cache.TryGetValue(cacheKey, out object? cached) && cached != null) return Ok(cached);

            var baseQuery = _context.WaterReadings.AsNoTracking();

            // Fix: Ensure the search includes the full day (up to 23:59:59)
            if (start.HasValue) baseQuery = baseQuery.Where(w => w.DateTime >= DateTime.SpecifyKind(start.Value, DateTimeKind.Utc));
            if (end.HasValue)
            {
                var finalEnd = DateTime.SpecifyKind(end.Value, DateTimeKind.Utc).Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                baseQuery = baseQuery.Where(w => w.DateTime <= finalEnd);
            }

            // Perform Daily Grouping in the Database
            var dailyQuery = baseQuery
                .GroupBy(w => w.DateTime.Date)
                .Select(g => new {
                    Date = g.Key,
                    ph = g.Average(w => w.PhLevel ?? 0),
                    nitrates = g.Average(w => w.Nitrates ?? 0),
                    phosphates = g.Average(w => w.Phosphates ?? 0),
                    ec = g.Average(w => w.ElectricalConductivity ?? 0),
                    ammonia = g.Average(w => w.Ammonia ?? 0)
                });

            int totalDays = await dailyQuery.CountAsync();
            if (totalDays == 0) return NotFound("No data found.");

            var paginatedDailyLogs = await dailyQuery
                .OrderByDescending(g => g.Date)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var maxSewage = await baseQuery.MaxAsync(w => (double?)w.Nitrates) ?? 0;
            var maxFertilizer = await baseQuery.MaxAsync(w => (double?)w.Phosphates) ?? 0;

            var phList = await baseQuery.Where(w => w.PhLevel != null)
                .OrderByDescending(w => w.DateTime)
                .Take(5000)
                .Select(w => (double)w.PhLevel!)
                .ToListAsync();
            double phVariance = phList.Count > 1 ? CalculateStdDev(phList) : 0;

            var result = new
            {
                auditMetadata = new
                {
                    totalRecordsAnalyzed = totalDays,
                    pagination = new
                    {
                        currentPage = pageNumber,
                        pageSize = pageSize,
                        totalPages = (int)Math.Ceiling(totalDays / (double)pageSize)
                    }
                },
                historicalExtremes = new { peakSewageInflowMgL = maxSewage, peakFertilizerInflowMgL = maxFertilizer },

                ecosystemVariance = new { phVarianceIndex = Math.Round(phVariance, 3) },

                // These are the Daily Averages
                telemetryLogs = paginatedDailyLogs.Select(l => new {
                    timestamp = l.Date,
                    ph = Math.Round(l.ph, 2),
                    nitrates = Math.Round(l.nitrates, 3),
                    phosphates = Math.Round(l.phosphates, 3),
                    ec = Math.Round(l.ec, 1),
                    ammonia = Math.Round(l.ammonia, 4)
                })
            };

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return Ok(result);
        }

        private string MapToTenLevels(double score, string type)
        {
            int index = (int)Math.Clamp(Math.Floor(score / 10.0), 0.0, 9.0);
            return type.ToLower() switch
            {
                "grade" => new[] { "1: Hazardous", "2: Critical", "3: Very Poor", "4: Poor", "5: Fair", "6: Average", "7: Good", "8: Very Good", "9: Excellent", "10: Pristine" }[index],
                "swim" => new[] { "Prohibited", "Highly Dangerous", "Danger", "Unsafe", "Not Recommended", "Caution", "Marginal", "Fair", "Safe", "Ideal" }[index],
                "stability" => new[] { "Chaotic", "Highly Volatile", "Unstable", "Shifting", "Fluctuating", "Improving", "Steadying", "Stable", "Highly Stable", "Rock Solid" }[index],
                "irritation" => new[] { "None", "Negligible", "Low", "Mild", "Moderate", "Noticeable", "High", "Very High", "Extreme", "Severe" }[index],
                "fish" => new[] { "None", "Very Low", "Unlikely", "Possible", "Probable", "High", "Very High", "Critical", "Massive", "Total Collapse" }[index],
                "odor" => new[] { "Fresh", "Neutral", "Earthy", "Musty", "Noticeable", "Strong", "Unpleasant", "Pungent", "Severe", "Overwhelming" }[index],
                "risk" => new[] { "Inert", "Negligible", "Very Low", "Low", "Moderate", "Elevated", "High", "Very High", "Extreme", "Outbreak Imminent" }[index],
                _ => "N/A"
            };
        }

        private float CalculateWQI(WaterReading data)
        {
            if (data.PhLevel == null || data.Nitrates == null || data.Phosphates == null) return 0;
            float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)data.PhLevel.Value) * 20f), 0f, 100f);
            float nitScore = Math.Clamp(100f - ((float)data.Nitrates.Value * 10f), 0f, 100f);
            float phoScore = Math.Clamp(100f - ((float)data.Phosphates.Value * 500f), 0f, 100f);
            return (float)Math.Round((phScore * 0.2f) + (nitScore * 0.4f) + (phoScore * 0.4f), 1);
        }

        private double CalculateStdDev(List<double> values)
        {
            if (values == null || values.Count < 2) return 0;
            double avg = values.Average();
            return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / values.Count);
        }
    }
}