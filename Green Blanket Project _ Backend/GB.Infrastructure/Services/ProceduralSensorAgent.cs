using GB.Infrastructure;
using GB.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GB.Infrastructure.Services
{
    public class ProceduralSensorAgent : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
        private readonly Random _rng = new();

        // Stateful Environment Memory
        private double _weatherIntensity = 0;
        private int _daysSinceLastMajorSpill = 0;

        public ProceduralSensorAgent(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine(">>> Procedural Sensor Agent is starting... <<<");

            // 1. Kick off the manual terminal listener in the background (fire-and-forget)
            _ = Task.Run(() => ListenForConsoleCommands(stoppingToken), stoppingToken);

            // 2. Start the automated loop
            while (!stoppingToken.IsCancellationRequested)
            {
                // ✅ TRAP 2 FIX: Inner Try-Catch ensures that a DB blip doesn't crash the entire app
                try
                {
                    await GenerateProceduralEntry(forceSpill: false, forcePristine: false);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[Agent Error] {DateTime.Now}: {ex.Message}");
                    Console.ResetColor();

                    // Wait 1 minute before retrying to let the database recover
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ListenForConsoleCommands(CancellationToken stoppingToken)
        {
            await Task.Delay(2000, stoppingToken);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=======================================================");
            Console.WriteLine(" 🛠️  AGENT MANUAL OVERRIDE ACTIVE");
            Console.WriteLine(" Press [T] to force a standard sensor reading.");
            Console.WriteLine(" Press [S] to force a severe sewage spill anomaly.");
            Console.WriteLine(" Press [P] to force a pristine (very good) water reading.");
            Console.WriteLine("=======================================================\n");
            Console.ResetColor();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).Key;

                        if (key == ConsoleKey.T)
                        {
                            Console.WriteLine("\n[Manual Override] ⚡ Forcing standard reading...");
                            await GenerateProceduralEntry(forceSpill: false, forcePristine: false);
                        }
                        else if (key == ConsoleKey.S)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n[Manual Override] ⚠️ INJECTING SEWAGE SPILL ANOMALY...");
                            Console.ResetColor();
                            _weatherIntensity = 1.0;
                            await GenerateProceduralEntry(forceSpill: true, forcePristine: false);
                        }
                        else if (key == ConsoleKey.P)
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine("\n[Manual Override] 🌊 INJECTING PRISTINE WATER CONDITIONS...");
                            Console.ResetColor();
                            _weatherIntensity = 0.0;
                            await GenerateProceduralEntry(forceSpill: false, forcePristine: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Override Error]: {ex.Message}");
                }

                await Task.Delay(100, stoppingToken);
            }
        }

        private async Task GenerateProceduralEntry(bool forceSpill, bool forcePristine = false)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GreenBlanketDbContext>();

            DateTime now = DateTime.UtcNow;
            DateTime sastTime = now.AddHours(2); // South African Standard Time

            int dayOfYear = sastTime.DayOfYear;
            double hour = sastTime.Hour + (sastTime.Minute / 60.0);
            double totalDays = (sastTime - new DateTime(2020, 1, 1)).TotalDays;

            // --- Climate & Weather Math ---
            double climateShift = (Math.Sin(totalDays / 365.25 / 3.1) + Math.Cos(totalDays / 365.25 / 1.7)) / 2.0;
            double season = Math.Cos((dayOfYear - 15) * 2 * Math.PI / 365);

            double rainProbability = season > 0 ? 0.08 : 0.01;
            if (_rng.NextDouble() < rainProbability)
            {
                _weatherIntensity += (_rng.NextDouble() * 0.4) - 0.2;
                _weatherIntensity = Math.Clamp(_weatherIntensity, 0, 1.0);
            }
            else
            {
                _weatherIntensity = Math.Max(0, _weatherIntensity - 0.01);
            }

            // --- Anomaly Logic ---
            bool isSewageSpill = forceSpill;
            if (!forcePristine)
            {
                _daysSinceLastMajorSpill++;
                if (!isSewageSpill && _rng.NextDouble() < 0.001 + (_weatherIntensity * 0.008))
                {
                    isSewageSpill = true;
                }
                if (isSewageSpill) _daysSinceLastMajorSpill = 0;
            }

            // --- Physical Metric Generation ---
            double phBase = 7.5 + (climateShift * 0.3);
            double phSwingMulti = 0.15 + (Math.Max(0, season) * 0.5);
            double finalPh = phBase + (Math.Sin((hour - 8) * 2 * Math.PI / 24) * phSwingMulti) + GenerateMicroJitter(0.05);

            double finalNitrates = Math.Max(0.1, 1.2 + (climateShift * 0.4) + (season * 0.3) + (_weatherIntensity * 3.0) + (isSewageSpill ? 5.0 : 0) + GenerateMicroJitter(0.1));
            double finalPhosphates = Math.Max(0.01, 0.1 + (climateShift * 0.05) + (isSewageSpill ? 0.9 : 0) + GenerateMicroJitter(0.02));
            double finalAmmonia = Math.Max(0.0, 0.05 + (isSewageSpill ? 1.8 : 0) + GenerateMicroJitter(0.01));

            double rainDilution = 1.0 - (_weatherIntensity * 0.4);
            double evapConcentration = (season * 5.0);

            double finalEc = Math.Max(10, ((60.0 - (climateShift * 5.0) + evapConcentration) * rainDilution) + GenerateMicroJitter(1.5));
            double finalSodium = Math.Max(5, ((45.0 + evapConcentration) * rainDilution) + GenerateMicroJitter(1.0));
            double finalCalcium = Math.Max(10, ((42.0 + (climateShift * 3.0)) * rainDilution) + GenerateMicroJitter(0.5));
            double finalMagnesium = Math.Max(2, ((15.0 + (climateShift * 2.0)) * rainDilution) + GenerateMicroJitter(0.3));
            double finalAlkalinity = Math.Max(30, 105.0 + (climateShift * 5.0) - (_weatherIntensity * 15.0) + GenerateMicroJitter(0.8));
            double finalSulfate = Math.Max(20, 80.0 + (climateShift * 15.0) + (_weatherIntensity * 20.0) + GenerateMicroJitter(2.0));
            double finalChloride = Math.Max(5, ((52.0 + evapConcentration) * rainDilution) + GenerateMicroJitter(1.0));

            // ✅ PRISTINE OVERRIDE: If the user presses [P], force scientifically clean conditions
            if (forcePristine)
            {
                finalPh = 7.2 + GenerateMicroJitter(0.05);
                finalNitrates = 0.1 + GenerateMicroJitter(0.02);
                finalPhosphates = 0.01 + GenerateMicroJitter(0.005);
                finalAmmonia = 0.0;
                finalEc = 25.0 + GenerateMicroJitter(1.0);
                finalSulfate = 20.0 + GenerateMicroJitter(1.0);
            }

            var reading = new WaterReading
            {
                DateTime = now,
                PhLevel = Math.Round(finalPh, 2),
                ElectricalConductivity = Math.Round(finalEc, 1),
                Nitrates = Math.Round(finalNitrates, 3),
                Phosphates = Math.Round(finalPhosphates, 4),
                Ammonia = Math.Round(finalAmmonia, 3),
                Calcium = Math.Round(finalCalcium, 1),
                Magnesium = Math.Round(finalMagnesium, 1),
                Sodium = Math.Round(finalSodium, 1),
                Chloride = Math.Round(finalChloride, 1),
                Sulfate = Math.Round(finalSulfate, 1),
                TotalAlkalinity = Math.Round(finalAlkalinity, 1)
            };

            context.WaterReadings.Add(reading);
            await context.SaveChangesAsync();

            // Status Console Output
            string weatherStatus = forcePristine ? "🌊 PRISTINE" : isSewageSpill ? "⚠️ SPILL" : _weatherIntensity > 0.5 ? "🌧️ RAIN" : "☀️ CLEAR";
            Console.WriteLine($"[Eco-Engine] {sastTime:HH:mm:ss} | pH: {reading.PhLevel:F1} | EC: {reading.ElectricalConductivity:F0} | NO3: {reading.Nitrates:F2} | {weatherStatus}");
        }

        private double GenerateMicroJitter(double scale) => (_rng.NextDouble() * scale * 2) - scale;
    }
}