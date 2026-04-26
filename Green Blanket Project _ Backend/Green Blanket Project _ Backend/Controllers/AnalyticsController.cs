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
        // 0. FOUNDATION (Database Connection Test)
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

        // ============================================================================
        // SIMPLE WATER QUALITY SCORE
        // ============================================================================
        [HttpGet("water-quality")]
        public async Task<IActionResult> GetWaterQualityScore()
        {
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("No recent water data available.");

            float wqi = CalculateWQI(latest);

            return Ok(new
            {
                waterQualityScore = Math.Round(wqi, 1)
            });
        }

        // ============================================================================
        // CHATBOT DATA FEED
        // ============================================================================
        [HttpGet("chatbot-summary")]
        public async Task<IActionResult> GetChatbotSummary()
        {
            // 1. Fetch latest data using AsNoTracking to avoid identity collapsing
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("No recent water data available for the chatbot.");

            // 2. Reuse the MASTER logic to ensure 100% consistency
            float wqi = CalculateWQI(latest);

            // Safety & Risk Calculations
            double ammoniaVal = latest.Ammonia ?? 0.05;
            double toxicPercentage = 1 / (Math.Pow(10, (9.25 - latest.PhLevel.Value)) + 1);
            double actualToxicNH3 = ammoniaVal * toxicPercentage;
            double healthRiskBase = Math.Clamp((Math.Max(0, latest.PhLevel.Value - 7.0) * 15) + (latest.Phosphates.Value * 120), 0, 100);

            // 3. Return a "System Prompt Friendly" Object
            return Ok(new
            {
                lastUpdated = latest.DateTime,

                // Primary Safety Metrics
                summary = new
                {
                    waterHealthScore = Math.Round(wqi, 1),
                    healthGrade = MapToTenLevels(wqi, "grade"),
                    swimSafetyStatus = MapToTenLevels(wqi, "swim"),
                    skinIrritationRisk = MapToTenLevels(healthRiskBase, "irritation"),
                    odorProfile = MapToTenLevels(latest.Phosphates.Value * 100, "odor")
                },

                // Scientific Context (for "Why" questions)
                scientificContext = new
                {
                    phValue = latest.PhLevel,
                    nitrateLevel = latest.Nitrates,
                    phosphateLevel = latest.Phosphates,
                    ammoniaToxicityMgL = Math.Round(actualToxicNH3, 5),
                    livestockSafety = actualToxicNH3 < 0.01 ? "Safe for animals" : "Danger: Toxic to livestock"
                },

                // Direct AI Instructions (Helps the LLM give better advice)
                aiGuidelines = new
                {
                    canISwim = wqi > 70 ? "Yes, conditions are optimal." : "Proceed with caution or avoid contact.",
                    healthWarning = healthRiskBase > 50 ? "Warning: High risk of skin rashes or ear infections." : "No significant health risks detected.",
                    currentConcern = latest.Phosphates > 0.1 ? "Heavy nutrient loading detected, likely fueling hyacinth growth." : "Nutrient levels are within normal historical ranges."
                }
            });
        }

        // ============================================================================
        // 1. OMNI-DASHBOARD (The Master Analytical Suite with 10-Level Granularity)
        // ============================================================================
        [HttpGet("omni-dashboard")]
        public async Task<IActionResult> GetOmniDashboard()
        {
            // Fetch Latest Data (Ensuring ALL metrics needed for math are NOT NULL)
            // We add .AsNoTracking() to prevent the "Identity Collapsing" bug
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient data found to build analytics.");

            // Ensure UTC conversion for consistent historical lookback
            DateTime latestUtc = DateTime.SpecifyKind(latest.DateTime, DateTimeKind.Utc);

            // Historical Query: Fetch up to 30 days of trends.
            // CRITICAL: .AsNoTracking() ensures 28 unique days show 28 unique values on your graph.
            var historicalData = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.DateTime >= latestUtc.AddDays(-30)
                         && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .OrderBy(w => w.DateTime)
                .ToListAsync();

            // --- CALCULATIONS: INTELLIGENCE ENGINE ---
            float wqi = CalculateWQI(latest);

            double ammoniaVal = latest.Ammonia ?? 0.05;
            double toxicPercentage = 1 / (Math.Pow(10, (9.25 - latest.PhLevel.Value)) + 1);
            double actualToxicNH3 = ammoniaVal * toxicPercentage;

            double expansionRate = (latest.Nitrates.Value * 0.5) + (latest.Phosphates.Value * 2.0);
            string sewageSource = (latest.Nitrates / (ammoniaVal > 0 ? ammoniaVal : 0.01)) > 10 ? "Leached Runoff" : "Active Raw Leak";

            var last7Days = historicalData.Where(d => d.DateTime >= latestUtc.AddDays(-7));
            double phVolatility = last7Days.Count() > 1 ? CalculateStdDev(last7Days.Select(d => (double)d.PhLevel.Value).ToList()) : 0;

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
                    // If you have multiple readings on the same day, consider adding HH:mm to the label
                    labels = historicalData.Select(d => d.DateTime.ToString("MMM dd")),
                    nitrateTrend = historicalData.Select(d => d.Nitrates),
                    phosphateTrend = historicalData.Select(d => d.Phosphates),
                    ecTrend = historicalData.Select(d => d.ElectricalConductivity)
                }
            });
        }

        // ============================================================================
        // 2. FILTERED HISTORICAL DATA (For Custom Range UI Graphs)
        // ============================================================================
        [HttpGet("history/range")]
        public async Task<IActionResult> GetRangeData([FromQuery] DateTime start, [FromQuery] DateTime end)
        {
            DateTime startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            DateTime endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc);
            var finalEnd = endUtc.AddHours(23).AddMinutes(59);

            var data = await _context.WaterReadings
                .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd)
                .OrderBy(w => w.DateTime)
                .Select(w => new { x = w.DateTime, ph = w.PhLevel, nitrates = w.Nitrates, phosphates = w.Phosphates, ec = w.ElectricalConductivity })
                .ToListAsync();

            if (!data.Any()) return NotFound("No data found in selection.");
            return Ok(new { count = data.Count, dataPoints = data });
        }

        // ============================================================
        // 3. ECOSYSTEM TRENDS
        // ============================================================
        [HttpGet("graph-data/critical-trends")]
        public async Task<IActionResult> GetCriticalTrends([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            // Force Swagger to look at February if you leave it blank
            DateTime startDate = start ?? new DateTime(2026, 02, 01);
            DateTime endDate = end ?? new DateTime(2026, 02, 28);

            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            DateTime endUtc = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            var finalEnd = endUtc.AddHours(23).AddMinutes(59);

            // 1. FETCH: Use .AsNoTracking() so the identical IDs don't merge
            var rawData = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd
                         && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .ToListAsync();

            if (!rawData.Any()) return NotFound("No data found.");

            // 2. PROCESS: Group by Date to ensure one dot per day
            var cleanTrend = rawData
                .GroupBy(d => d.DateTime.Date)
                .OrderBy(g => g.Key)
                .Select(g => new {
                    Date = g.Key,
                    Ph = g.Average(x => x.PhLevel.Value),
                    Nitrates = g.Average(x => x.Nitrates.Value),
                    Phosphates = g.Average(x => x.Phosphates.Value),
                    Ammonia = g.Average(x => x.Ammonia ?? 0.05)
                })
                .ToList();

            return Ok(new
            {
                count = cleanTrend.Count, // This should now say 27!
                labels = cleanTrend.Select(d => d.Date.ToString("MMM dd")),
                vitalityTrend = cleanTrend.Select(d => {
                    float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)d.Ph) * 20f), 0, 100);
                    float nitrateScore = Math.Clamp(100f - ((float)d.Nitrates * 10f), 0, 100);
                    float phosphateScore = Math.Clamp(100f - ((float)d.Phosphates * 500f), 0, 100);
                    return Math.Round((phScore * 0.2f) + (nitrateScore * 0.4f) + (phosphateScore * 0.4f), 1);
                }),
                nutrientTrend = cleanTrend.Select(d => new {
                    nitrates = Math.Round(d.Nitrates, 3),
                    phosphates = Math.Round(d.Phosphates, 3)
                }),
                safetyTrend = cleanTrend.Select(d => {
                    double toxicPercentage = 1 / (Math.Pow(10, (9.25 - d.Ph)) + 1);
                    return Math.Round(d.Ammonia * toxicPercentage, 4);
                })
            });
        }

        // ============================================================
        // 4. FORENSIC ATTRIBUTION API (Detects the SOURCE of pollution)
        // ============================================================
        [HttpGet("forensic-attribution")]
        public async Task<IActionResult> GetForensicAttribution([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            // Default to the February 2026 dataset if no dates provided
            DateTime startDate = start ?? new DateTime(2026, 02, 01);
            DateTime endDate = end ?? new DateTime(2026, 02, 28);

            DateTime startUtc = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            DateTime endUtc = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            var finalEnd = endUtc.AddHours(23).AddMinutes(59);

            var rawData = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.DateTime >= startUtc && w.DateTime <= finalEnd
                         && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.ElectricalConductivity != null)
                .ToListAsync();

            if (!rawData.Any()) return NotFound("No forensic data found for this range.");

            // 1. Calculations: Source Attribution
            double avgNitrates = rawData.Average(d => d.Nitrates.Value);
            double avgPhosphates = rawData.Average(d => d.Phosphates.Value);
            double avgEC = rawData.Average(d => d.ElectricalConductivity.Value);

            // Redfield logic: Ratio < 16 is Nitrogen-limited (Sewage); > 16 is Phosphorus-limited (Agriculture)
            double overallRatio = avgNitrates / (avgPhosphates > 0 ? avgPhosphates : 0.01);

            // Detect "Illegal Dumps" (Conductivity spikes above 85 mS/m)
            int industrialEvents = rawData.Count(d => d.ElectricalConductivity > 85);

            // 2. Health Stability (Standard Deviation of pH)
            double pHVariance = CalculateStdDev(rawData.Select(d => (double)d.PhLevel.Value).ToList());

            return Ok(new
            {
                analysisPeriod = $"{startDate:MMM dd} - {endDate:MMM dd}",
                attributionSummary = new
                {
                    dominantPolluter = overallRatio < 16 ? "Municipal (Wastewater Treatment Failures)" : "Agricultural (Fertilizer Runoff)",
                    sewageLoadIndex = Math.Round(avgNitrates * 10, 1), // High Nitrates = Sewage
                    fertilizerLoadIndex = Math.Round(avgPhosphates * 100, 1), // High Phosphates = Fertilizer
                    industrialRiskLevel = industrialEvents > 5 ? "High" : (industrialEvents > 0 ? "Moderate" : "Low")
                },
                forensicMetrics = new
                {
                    avgRedfieldRatio = Math.Round(overallRatio, 2),
                    illegalDischargeEventsDetected = industrialEvents,
                    ecosystemStabilityIndex = Math.Round(100 - (pHVariance * 40), 1), // Higher = more stable
                    trophicStatus = avgPhosphates > 0.1 ? "Hyper-eutrophic" : "Eutrophic"
                },
                recommendation = industrialEvents > 3
                    ? "Alert: Frequent industrial conductivity spikes detected. Notify DWS for upstream monitoring."
                    : "Focus on upstream wastewater treatment plant remediation."
            });
        }

        // ============================================================
        // 5. REMEDIATION PROGRESS API (Measures cleanup performance)
        // ============================================================
        [HttpGet("remediation-progress")]
        public async Task<IActionResult> GetRemediationProgress([FromQuery] DateTime? end)
        {
            // 1. Set Windows: Compare 'Last 7 Days' vs 'Previous 7 Days'
            var latestInDb = await _context.WaterReadings.MaxAsync(w => (DateTime?)w.DateTime) ?? DateTime.UtcNow;
            DateTime currentEnd = end ?? latestInDb;
            DateTime currentStart = currentEnd.AddDays(-7);

            DateTime baselineEnd = currentStart.AddSeconds(-1);
            DateTime baselineStart = baselineEnd.AddDays(-7);

            // 2. Fetch Data Sets
            var currentData = await _context.WaterReadings.AsNoTracking()
                .Where(w => w.DateTime >= currentStart && w.DateTime <= currentEnd
                         && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .ToListAsync();

            var baselineData = await _context.WaterReadings.AsNoTracking()
                .Where(w => w.DateTime >= baselineStart && w.DateTime <= baselineEnd
                         && w.PhLevel != null && w.Nitrates != null && w.Phosphates != null)
                .ToListAsync();

            if (currentData.Count < 3 || baselineData.Count < 3)
                return NotFound("Insufficient data to generate a comparative progress report.");

            // 3. Calculate Averages
            double currentWqi = currentData.Average(d => (double)CalculateWQI(d));
            double baselineWqi = baselineData.Average(d => (double)CalculateWQI(d));

            double currentNutrients = currentData.Average(d => d.Nitrates.Value + d.Phosphates.Value);
            double baselineNutrients = baselineData.Average(d => d.Nitrates.Value + d.Phosphates.Value);

            // 4. Calculate Deltas (%)
            double healthDelta = ((currentWqi - baselineWqi) / (baselineWqi > 0 ? baselineWqi : 1)) * 100;
            double pollutionDelta = ((currentNutrients - baselineNutrients) / (baselineNutrients > 0 ? baselineNutrients : 1)) * 100;

            return Ok(new
            {
                comparisonWindow = $"{baselineStart:MMM dd} vs {currentStart:MMM dd}",
                summary = new
                {
                    ecosystemStatus = healthDelta > 5 ? "Improving" : (healthDelta < -5 ? "Declining" : "Stable"),
                    remediationConfidence = currentData.Count > 10 ? "High" : "Moderate"
                },
                vitalityImprovement = new
                {
                    baselineScore = Math.Round(baselineWqi, 1),
                    currentScore = Math.Round(currentWqi, 1),
                    percentageGain = Math.Round(healthDelta, 1),
                    indicator = healthDelta >= 0 ? "UP" : "DOWN"
                },
                pollutionReduction = new
                {
                    baselineLoad = Math.Round(baselineNutrients, 2),
                    currentLoad = Math.Round(currentNutrients, 2),
                    percentageReduced = Math.Round(-pollutionDelta, 1), // Positive means reduction
                    isTargetMet = currentNutrients < baselineNutrients
                },
                forecast = new
                {
                    weeksToTargetHealth = healthDelta > 0 ? Math.Ceiling((80 - currentWqi) / (healthDelta / 7)) : 0,
                    recommendation = pollutionDelta > 0
                        ? "Warning: Inflow is outpacing remediation. Check upstream spillages."
                        : "Remediation is effective. Maintain nanobubble saturation."
                }
            });
        }

        // ============================================================
        // 6. BLOOM RISK ENGINE (The Proactive Early Warning System)
        // ============================================================
        [HttpGet("bloom-forecast")]
        public async Task<IActionResult> GetBloomForecast()
        {
            // 1. Fetch the last 48 hours of data to detect chemical "momentum"
            var latestDate = await _context.WaterReadings.MaxAsync(w => (DateTime?)w.DateTime) ?? DateTime.UtcNow;

            var recentWindow = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.DateTime >= latestDate.AddDays(-2)
                         && w.PhLevel != null
                         && w.Nitrates != null
                         && w.Phosphates != null)
                .ToListAsync();

            if (recentWindow.Count < 2)
                return NotFound("Insufficient recent data points to calculate chemical volatility for a forecast.");

            // Order data to identify the most recent state
            var sortedWindow = recentWindow.OrderBy(w => w.DateTime).ToList();
            var latestRecord = sortedWindow.Last();

            // 2. Logic Part A: Nutrient Saturation ("The Fuel Tank")
            // High Phosphates (>0.15) and Nitrates (>5) act as rocket fuel for hyacinth
            double nutrientFuel = (latestRecord.Phosphates.Value * 10) + (latestRecord.Nitrates.Value / 2);

            // 3. Logic Part B: Chemical Volatility ("The Spark")
            // We calculate shift as (Max - Min) in the 48hr window. 
            // High pH swings indicate rapid photosynthesis or active pollution inflows.
            double phMax = recentWindow.Max(w => w.PhLevel.Value);
            double phMin = recentWindow.Min(w => w.PhLevel.Value);
            double phShift = phMax - phMin;

            // 4. Calculate Final Probability (0-100%)
            // Weighted: 50% on existing nutrients, 50% on how fast the chemistry is moving
            double probability = Math.Clamp((nutrientFuel * 5) + (phShift * 60), 0, 100);

            return Ok(new
            {
                forecastGeneratedAt = DateTime.Now,
                latestReadingTime = latestRecord.DateTime,

                riskMetrics = new
                {
                    bloomProbability = Math.Round(probability, 1) + "%",
                    currentRiskLevel = MapToTenLevels(probability, "risk"), // Returns "Low", "High", etc.
                    daysUntilSaturation = probability > 80 ? "Immediate / Active" : (probability > 50 ? "2-3 Days" : "Stable window"),
                },

                triggerFactors = new
                {
                    isNutrientSaturated = latestRecord.Phosphates > 0.15,
                    isRapidlyShifting = phShift > 0.4, // Shift of >0.4 in 48h is considered high volatility
                    primaryGrowthDriver = (latestRecord.Nitrates / latestRecord.Phosphates < 16) ? "Nitrogen (Sewage)" : "Phosphorus (Runoff)"
                },

                actionPlan = probability > 70
                    ? "CRITICAL: Environmental conditions optimized for bloom. Deploy harvesting teams to harbors."
                    : "Ecosystem is currently within manageable growth parameters. Monitor for pH spikes."
            });
        }

        // ============================================================
        // 7. AGRICULTURAL INTELLIGENCE (Irrigation & Soil Safety)
        // ============================================================
        [HttpGet("irrigation-safety")]
        public async Task<IActionResult> GetIrrigationSafety()
        {
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.Calcium != null && w.Magnesium != null && w.Sodium != null && w.ElectricalConductivity != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient mineral data for agricultural analysis.");

            // 1. Convert mg/L to meq/L (Milliequivalents per liter) for the SAR formula
            double na_meq = latest.Sodium.Value / 22.99;
            double ca_meq = latest.Calcium.Value / 20.04;
            double mg_meq = latest.Magnesium.Value / 12.15;

            // 2. The SAR Formula (Sodium Adsorption Ratio)
            double sar = na_meq / Math.Sqrt((ca_meq + mg_meq) / 2);

            // 3. Salinity Risk (Based on EC)
            string salinityRisk = latest.ElectricalConductivity > 75 ? "High (Crop Stress)" :
                                 (latest.ElectricalConductivity > 25 ? "Medium" : "Low");

            return Ok(new
            {
                timestamp = latest.DateTime,
                soilHealthMetrics = new
                {
                    sodiumAdsorptionRatio = Math.Round(sar, 2),
                    sarStatus = sar < 3 ? "Excellent" : (sar < 6 ? "Good" : "Warning: Soil Permeability Risk"),
                    salinityHazard = salinityRisk
                },
                cropSuitability = new
                {
                    citrusSafety = (sar < 3 && latest.ElectricalConductivity < 50) ? "Optimal" : "Risk of Leaf Burn",
                    tobaccoSafety = latest.ElectricalConductivity < 40 ? "Safe" : "Dangerous (High Chloride Risk)",
                    generalGrains = "Safe for Maize and Wheat"
                },
                recommendation = sar > 5
                    ? "Warning: Continued use may lead to soil sodicity. Apply Gypsum remediation to fields."
                    : "Water is chemically balanced for long-term irrigation."
            });
        }

        // ============================================================
        // 8. ASSET PROTECTION API (Corrosivity & Organic Decay)
        // ============================================================
        [HttpGet("infrastructure-risk")]
        public async Task<IActionResult> GetInfrastructureRisk()
        {
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.Chloride != null && w.Sulfate != null && w.TotalAlkalinity != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient mineral data for infrastructure analysis.");

            // 1. Engineering Math: Larson-Skold Index
            // Formula: (Chloride + Sulfate) / Alkalinity (in meq/L)
            double cl_meq = latest.Chloride.Value / 35.45;
            double so4_meq = (latest.Sulfate.Value / 96.06) * 2;
            double alk_meq = latest.TotalAlkalinity.Value / 50.04;

            double larsonIndex = (cl_meq + so4_meq) / (alk_meq > 0 ? alk_meq : 0.01);

            // 2. Organic Decay Impact (Decaying hyacinth/algae)
            double organicLoad = latest.KjeldahlNitrogen ?? 0.5;

            return Ok(new
            {
                timestamp = latest.DateTime,
                corrosivityAnalysis = new
                {
                    larsonSkoldIndex = Math.Round(larsonIndex, 2),
                    metalRiskLevel = larsonIndex < 0.6 ? "Low" : (larsonIndex < 1.2 ? "Moderate" : "High (Active Corrosion)"),
                    impactDescription = "Measures the aggressive nature of water toward boat motors and steel infrastructure."
                },
                organicDecayStatus = new
                {
                    organicNitrogenMgL = organicLoad,
                    decompositionRate = organicLoad > 2.5 ? "Rapid (Post-Bloom Rot)" : "Normal",
                    oxygenDepletionRisk = organicLoad > 3.0 ? "Severe (Fish Kill Hazard)" : "Low"
                },
                maintenanceAdvice = new
                {
                    boatOwners = larsonIndex > 1.0 ? "Flush outboard motors with fresh water immediately after use." : "Standard maintenance.",
                    estateManagers = larsonIndex > 1.2 ? "High risk to galvanised pipes. Inspect submerged pumps for pitting." : "No immediate action required.",
                    waterAuthority = organicLoad > 2.0 ? "Biomass decay detected. Increase aeration to prevent H2S gas formation." : "Organic levels stable."
                }
            });
        }

        // ============================================================
        // 9. CIRCULAR ECONOMY API (Pollution to Profit)
        // ============================================================
        [HttpGet("nutrient-harvest-value")]
        public async Task<IActionResult> GetHarvestValue()
        {
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.Nitrates != null && w.Phosphates != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Data unavailable for economic modeling.");

            // 1. Engineering Constants for Hartbeespoort
            double reservoirVolumeM3 = 195000000; // Average volume of Harties in cubic meters
            double currentMarketPricePerTonN = 18500.00; // Estimated Rand per Ton for Nitrogen
            double currentMarketPricePerTonP = 24000.00; // Estimated Rand per Ton for Phosphorus

            // 2. Calculate Total Mass in the Reservoir (mg/L to Tonnes)
            // (mg/L is same as grams per cubic meter)
            double totalTonnesN = (latest.Nitrates.Value * reservoirVolumeM3) / 1000000;
            double totalTonnesP = (latest.Phosphates.Value * reservoirVolumeM3) / 1000000;

            // 3. Estimated Harvestable Biomass Value
            // Not all nutrients can be captured, assume a 15% harvest efficiency
            double financialValue = (totalTonnesN * currentMarketPricePerTonN + totalTonnesP * currentMarketPricePerTonP) * 0.15;

            return Ok(new
            {
                timestamp = latest.DateTime,
                resourceMass = new
                {
                    nitrogenTonnes = Math.Round(totalTonnesN, 1),
                    phosphorusTonnes = Math.Round(totalTonnesP, 1),
                    totalRecoverableBiomass = Math.Round((totalTonnesN + totalTonnesP) * 8.5, 0) // Plants weigh 8.5x more than raw nutrients
                },
                marketValue = new
                {
                    currency = "ZAR",
                    estimatedHarvestValue = Math.Round(financialValue, 2),
                    valuePerHectare = Math.Round(financialValue / 2000, 2) // Based on dam surface area
                },
                dataConfidence = new
                {
                    assuranceLevel = latest.QualityTag ?? "Standard",
                    isReliable = latest.QualityTag == "1" || latest.QualityTag == "A"
                },
                businessCase = $"The reservoir currently holds {Math.Round(totalTonnesN, 1)} tonnes of Nitrogen waste. Harvesting this as organic fertilizer could offset remediation costs by {Math.Round(financialValue / 1000000, 2)} Million Rand."
            });
        }

        // ============================================================
        // 10. REGULATORY COMPLIANCE API (Legal & Accountability)
        // ============================================================
        [HttpGet("regulatory-compliance")]
        public async Task<IActionResult> GetComplianceStatus()
        {
            var latest = await _context.WaterReadings
                .AsNoTracking()
                .Where(w => w.PhLevel != null && w.Nitrates != null && w.Phosphates != null && w.Ammonia != null)
                .OrderByDescending(w => w.DateTime)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Insufficient data for regulatory audit.");

            // 1. Define DWS (Department of Water & Sanitation) Thresholds
            bool phBreach = latest.PhLevel.Value < 6.5 || latest.PhLevel.Value > 9.0;
            bool phosphateBreach = latest.Phosphates.Value > 0.05; // The famous Harties '0.05' limit
            bool nitrateBreach = latest.Nitrates.Value > 6.0;
            bool ammoniaBreach = latest.Ammonia.Value > 0.02;

            // 2. Calculate Compliance Score (Deduction based)
            double score = 100;
            if (phBreach) score -= 15;
            if (phosphateBreach) score -= 35; // Heavy weight for phosphates
            if (nitrateBreach) score -= 25;
            if (ammoniaBreach) score -= 25;

            // 3. Determine Accountability
            string accountability = (latest.Nitrates / latest.Phosphates < 16)
                ? "Municipal (Waste Water Treatment Works)"
                : "Catchment Management (Agricultural/Industrial)";

            return Ok(new
            {
                auditTimestamp = latest.DateTime,
                complianceOverview = new
                {
                    overallComplianceScore = Math.Max(0, score),
                    rating = score > 80 ? "Compliant" : (score > 50 ? "Non-Compliant" : "Critical Violation"),
                    dataVerification = latest.QualityTag == "1" ? "Verified Lab Result" : "Raw IoT Telemetry"
                },
                violationDetails = new
                {
                    phosphateStatus = phosphateBreach ? $"EXCEEDED (Current: {latest.Phosphates} vs Limit: 0.05)" : "Within Limit",
                    ammoniaStatus = ammoniaBreach ? $"EXCEEDED (Current: {latest.Ammonia} vs Limit: 0.02)" : "Within Limit",
                    nitrateStatus = nitrateBreach ? "Exceeded Threshold" : "Within Limit"
                },
                accountabilityMetadata = new
                {
                    primaryResponsibleSector = accountability,
                    regulatoryBody = "Department of Water and Sanitation (DWS)",
                    legalPrecedent = "SAHRC Report on Vaal/Hartbeespoort Catchment Failures"
                },
                actionRequired = score < 50
                    ? "Immediate Action: File a formal environmental non-compliance report with the DWS Blue Drop inspectors."
                    : "Maintain routine monitoring."
            });
        }

        // ============================================================
        // 11. MASTER STATISTICAL AUDIT (The 'Big Data' Overview)
        // ============================================================
        [HttpGet("master-audit")]
        public async Task<IActionResult> GetMasterAudit([FromQuery] DateTime? start, [FromQuery] DateTime? end)
        {
            // 1. Build Query with .AsNoTracking() for high performance on mass data
            var query = _context.WaterReadings.AsNoTracking();

            // Fix UTC kinds for PostgreSQL compatibility if dates are provided
            if (start.HasValue)
                query = query.Where(w => w.DateTime >= DateTime.SpecifyKind(start.Value, DateTimeKind.Utc));

            if (end.HasValue)
                query = query.Where(w => w.DateTime <= DateTime.SpecifyKind(end.Value, DateTimeKind.Utc));

            var massData = await query.ToListAsync();

            if (!massData.Any())
                return NotFound("No historical records found for the requested audit window.");

            // 2. Extreme Value Analysis (The All-Time Hall of Records)
            // We use .Max() while handling potential nulls
            var maxPh = massData.Max(d => d.PhLevel ?? 0);
            var maxNitrate = massData.Max(d => d.Nitrates ?? 0);
            var maxPhosphate = massData.Max(d => d.Phosphates ?? 0);
            var maxAmmonia = massData.Max(d => d.Ammonia ?? 0);

            // 3. Statistical Health: Measuring Stability
            // We calculate the Standard Deviation to see how much the water 'swings'
            double phStability = CalculateStdDev(massData.Select(d => d.PhLevel ?? 7.0).ToList());
            double nutrientVolatility = CalculateStdDev(massData.Select(d => d.Nitrates ?? 0.0).ToList());

            // 4. Data Integrity Reporting
            // Determines what % of your database rows are actually usable (not empty)
            int totalFieldsExpected = massData.Count * 4; // Checking 4 core metrics per row
            int actualDataCells = massData.Count(d => d.PhLevel != null) +
                                  massData.Count(d => d.Nitrates != null) +
                                  massData.Count(d => d.Phosphates != null) +
                                  massData.Count(d => d.Ammonia != null);

            double completeness = ((double)actualDataCells / totalFieldsExpected) * 100;

            return Ok(new
            {
                auditMetadata = new
                {
                    totalRecordsAnalyzed = massData.Count,
                    earliestEntry = massData.Min(d => d.DateTime),
                    latestEntry = massData.Max(d => d.DateTime),
                    reportGeneratedAt = DateTime.Now
                },

                historicalExtremes = new
                {
                    highestAlkalinityRecorded = maxPh,
                    peakSewageInflowMgL = maxNitrate,
                    peakFertilizerInflowMgL = maxPhosphate,
                    lethalAmmoniaSpikeMgL = maxAmmonia
                },

                ecosystemVariance = new
                {
                    phVarianceIndex = Math.Round(phStability, 3),
                    nutrientVolatilityScore = Math.Round(nutrientVolatility, 3),
                    description = "High variance ( > 0.8) indicates an ecosystem in active distress or transition."
                },

                databaseHealth = new
                {
                    completenessPercentage = Math.Round(completeness, 1) + "%",
                    sensorHealthStatus = massData.Count > 50 ? "High Fidelity" : "Low Resolution",
                    isUsableForModeling = completeness > 85
                },

                scientificConclusion = $"Statistical analysis of {massData.Count} points indicates that the dam is " +
                $"{(phStability > 0.6 ? "undergoing rapid chemical shifts" : "chemically stable")}. The historical peak nitrate of {maxNitrate} mg/L remains the primary environmental bottleneck."
            });
        }

        // ==========================================
        // HELPERS
        // ==========================================

        /// <summary>
        /// Translates a numerical score (0-100) into a human-readable 10-level scale.
        /// Used for Gauges, Labels, and UI Indicators.

        private string MapToTenLevels(double score, string type)
        {
            // Logic: Clamps the score and divides by 10 to get an index (0-9)
            int index = (int)Math.Clamp(Math.Floor(score / 10), 0, 9);

            return type.ToLower() switch
            {
                // API 1: Health Grades
                "grade" => new[] { "1: Hazardous", "2: Critical", "3: Very Poor", "4: Poor", "5: Fair", "6: Average", "7: Good", "8: Very Good", "9: Excellent", "10: Pristine" }[index],

                // API 1: Swimming Safety
                "swim" => new[] { "Prohibited", "Highly Dangerous", "Danger", "Unsafe", "Not Recommended", "Caution", "Marginal", "Fair", "Safe", "Ideal" }[index],

                // API 1: Skin Irritation Risks (Blue-Green Algae)
                "irritation" => new[] { "None", "Negligible", "Low", "Mild", "Moderate", "Noticeable", "High", "Very High", "Extreme", "Severe" }[index],

                // API 1: Ecological Impact (Fish Survival)
                "fish" => new[] { "None", "Very Low", "Unlikely", "Possible", "Probable", "High", "Very High", "Critical", "Massive", "Total Collapse" }[index],

                // API 1: Shoreline Odor (Rotting organic matter)
                "odor" => new[] { "Fresh", "Neutral", "Earthy", "Musty", "Noticeable", "Strong", "Unpleasant", "Pungent", "Severe", "Overwhelming" }[index],

                // API 4 & 11: Ecosystem Stability Index
                "stability" => new[] { "Chaotic", "Highly Volatile", "Unstable", "Shifting", "Fluctuating", "Improving", "Steadying", "Stable", "Highly Stable", "Rock Solid" }[index],

                // API 6: Bloom Risk Probability (The Fix for 'Unknown')
                "risk" => new[] { "Inert", "Negligible", "Very Low", "Low", "Moderate", "Elevated", "High", "Very High", "Extreme", "Outbreak Imminent" }[index],

                _ => "Information Unavailable"
            };
        }

        /// <summary>
        /// Calculates the overall Water Quality Index (WQI) based on weighted chemical parameters.

        private float CalculateWQI(WaterReading data)
        {
            // Defensive Check: If required chemicals are missing, return 0 (Unrated)
            if (data.PhLevel == null || data.Nitrates == null || data.Phosphates == null) return 0;

            // 1. Ph Score (Optimized for 7.5 neutrality)
            float phScore = Math.Clamp(100f - (Math.Abs(7.5f - (float)data.PhLevel.Value) * 20f), 0, 100);

            // 2. Nitrate Score (Penalizes levels above 0)
            float nitrateScore = Math.Clamp(100f - ((float)data.Nitrates.Value * 10f), 0, 100);

            // 3. Phosphate Score (Heavy penalty: Harties limit is 0.05)
            float phosphateScore = Math.Clamp(100f - ((float)data.Phosphates.Value * 500f), 0, 100);

            // Weighting: Nutrients (80%) are the primary health drivers, pH is 20%
            double finalScore = (phScore * 0.2f) + (nitrateScore * 0.4f) + (phosphateScore * 0.4f);

            return (float)Math.Round(finalScore, 1);
        }

        /// <summary>
        /// Statistical Engine: Calculates Population Standard Deviation.
        /// Used to measure volatility and ecosystem shifts.
  
        private double CalculateStdDev(List<double> values)
        {
            if (values == null || values.Count < 2) return 0;

            double avg = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));

            return Math.Sqrt(sumOfSquares / values.Count);
        }
    }
}

